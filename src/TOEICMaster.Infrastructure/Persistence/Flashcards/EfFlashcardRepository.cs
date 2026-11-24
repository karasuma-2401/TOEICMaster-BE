using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Flashcards;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Flashcards;

public sealed class EfFlashcardRepository(ToeicDbContext dbContext) : IFlashcardRepository
{
    public async Task<IReadOnlyList<FlashcardDeck>> GetVisibleDecksAsync(
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FlashcardDecks
            .AsNoTracking()
            .Include(deck => deck.Cards)
            .AsSplitQuery()
            .Where(deck => deck.OwnerUserId == null || (userId.HasValue && deck.OwnerUserId == userId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<FlashcardDeck?> GetVisibleDeckBySlugAsync(
        string slug,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FlashcardDecks
            .AsNoTracking()
            .Include(deck => deck.Cards)
            .FirstOrDefaultAsync(
                deck => deck.Slug == slug
                    && (deck.OwnerUserId == null || (userId.HasValue && deck.OwnerUserId == userId.Value)),
                cancellationToken);
    }

    public Task<bool> DeckVisibleToUserAsync(
        Guid userId,
        Guid deckId,
        CancellationToken cancellationToken) =>
        dbContext.FlashcardDecks
            .AsNoTracking()
            .AnyAsync(
                deck => deck.Id == deckId && (deck.OwnerUserId == null || deck.OwnerUserId == userId),
                cancellationToken);

    public async Task<bool> CardExistsVisibleToUserAsync(
        Guid cardId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FlashcardCards
            .AsNoTracking()
            .Join(
                dbContext.FlashcardDecks.AsNoTracking(),
                card => card.DeckId,
                deck => deck.Id,
                (card, deck) => new { card.Id, deck.OwnerUserId })
            .AnyAsync(
                item => item.Id == cardId
                    && (item.OwnerUserId == null || item.OwnerUserId == userId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserFlashcardProgress>> GetUserProgressAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserFlashcardProgress
            .AsNoTracking()
            .Where(progress => progress.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserFlashcardMatchRecord>> GetUserMatchRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserFlashcardMatchRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken)
    {
        dbContext.FlashcardDecks.Add(deck);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteOwnedDeckAsync(
        Guid userId,
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var deleted = await dbContext.FlashcardDecks
            .Where(deck => deck.Id == deckId && deck.OwnerUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<FlashcardDeckAddCardResult?> AddCardToOwnedDeckAsync(
        Guid userId,
        Guid deckId,
        FlashcardCardInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var lockKey = ComputeDeckLockKey(deckId);
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async retryToken =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(retryToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})", retryToken);

            var deck = await dbContext.FlashcardDecks
                .Include(d => d.Cards)
                .FirstOrDefaultAsync(d => d.Id == deckId && d.OwnerUserId == userId, retryToken);

            if (deck is null)
            {
                await transaction.RollbackAsync(retryToken);
                return null;
            }

            if (deck.Cards.Count >= FlashcardDeck.MaxCardsPerDeck)
            {
                await transaction.RollbackAsync(retryToken);
                throw new ValidationException([
                    new ValidationFailure(
                        "Cards",
                        $"Each deck can contain at most {FlashcardDeck.MaxCardsPerDeck} cards.")
                ]);
            }

            var card = deck.AddCard(input, nowUtc);
            dbContext.FlashcardCards.Add(card);
            await dbContext.SaveChangesAsync(retryToken);
            await transaction.CommitAsync(retryToken);

            return new FlashcardDeckAddCardResult(deck.Slug, card);
        }, cancellationToken);
    }

    public async Task<UserFlashcardProgress> UpsertProgressAtomicAsync(
        Guid userId,
        Guid cardId,
        string status,
        bool starred,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var initialReviewCount = status == FlashcardMasteryStatus.New ? 0 : 1;

        var rows = await dbContext.UserFlashcardProgress
            .FromSqlInterpolated($@"
                INSERT INTO user_flashcard_progress (""Id"", ""UserId"", ""CardId"", ""Status"", ""Starred"", ""ReviewCount"", ""UpdatedAtUtc"")
                VALUES ({id}, {userId}, {cardId}, {status}, {starred}, {initialReviewCount}, {nowUtc})
                ON CONFLICT (""UserId"", ""CardId"") DO UPDATE
                SET ""ReviewCount"" = CASE
                        WHEN EXCLUDED.""Status"" <> {FlashcardMasteryStatus.New}
                             AND (EXCLUDED.""Status"" <> user_flashcard_progress.""Status""
                                  OR EXCLUDED.""Starred"" = user_flashcard_progress.""Starred"")
                        THEN user_flashcard_progress.""ReviewCount"" + 1
                        ELSE user_flashcard_progress.""ReviewCount""
                    END,
                    ""Status"" = EXCLUDED.""Status"",
                    ""Starred"" = EXCLUDED.""Starred"",
                    ""UpdatedAtUtc"" = EXCLUDED.""UpdatedAtUtc""
                RETURNING ""Id"", ""UserId"", ""CardId"", ""Status"", ""Starred"", ""ReviewCount"", ""UpdatedAtUtc""
            ")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows[0];
    }

    public async Task<(UserFlashcardMatchRecord Record, bool IsNewRecord)> RecordMatchAtomicAsync(
        Guid userId,
        Guid deckId,
        int elapsedMs,
        int maxCombo,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var previousBest = await dbContext.UserFlashcardMatchRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && record.DeckId == deckId)
            .Select(record => (int?)record.BestTimeMs)
            .FirstOrDefaultAsync(cancellationToken);

        var id = Guid.NewGuid();
        var rows = await dbContext.UserFlashcardMatchRecords
            .FromSqlInterpolated($@"
                INSERT INTO user_flashcard_match_records (""Id"", ""UserId"", ""DeckId"", ""BestTimeMs"", ""BestCombo"", ""PlayCount"", ""UpdatedAtUtc"")
                VALUES ({id}, {userId}, {deckId}, {elapsedMs}, {maxCombo}, 1, {nowUtc})
                ON CONFLICT (""UserId"", ""DeckId"") DO UPDATE
                SET ""BestTimeMs"" = LEAST(user_flashcard_match_records.""BestTimeMs"", EXCLUDED.""BestTimeMs""),
                    ""BestCombo"" = GREATEST(user_flashcard_match_records.""BestCombo"", EXCLUDED.""BestCombo""),
                    ""PlayCount"" = user_flashcard_match_records.""PlayCount"" + 1,
                    ""UpdatedAtUtc"" = EXCLUDED.""UpdatedAtUtc""
                RETURNING ""Id"", ""UserId"", ""DeckId"", ""BestTimeMs"", ""BestCombo"", ""PlayCount"", ""UpdatedAtUtc""
            ")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var saved = rows[0];
        var isNewRecord = previousBest is null || elapsedMs < previousBest.Value;
        return (saved, isNewRecord);
    }

    public Task DeleteDeckProgressAsync(
        Guid userId,
        Guid deckId,
        CancellationToken cancellationToken)
    {
        return dbContext.UserFlashcardProgress
            .Where(progress =>
                progress.UserId == userId &&
                dbContext.FlashcardCards.Any(card => card.Id == progress.CardId && card.DeckId == deckId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static long ComputeDeckLockKey(Guid deckId)
    {
        Span<byte> bytes = stackalloc byte[16];
        deckId.TryWriteBytes(bytes);
        var low = BitConverter.ToInt64(bytes[..8]);
        var high = BitConverter.ToInt64(bytes[8..]);
        return low ^ high;
    }
}
