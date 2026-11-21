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
        var query = dbContext.FlashcardDecks
            .AsNoTracking()
            .Include(deck => deck.Cards)
            .Where(deck => deck.OwnerUserId == null || (userId.HasValue && deck.OwnerUserId == userId.Value));

        return await query.ToListAsync(cancellationToken);
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

    public async Task<FlashcardDeck?> GetDeckByIdAsync(
        Guid deckId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FlashcardDecks
            .Include(deck => deck.Cards)
            .FirstOrDefaultAsync(deck => deck.Id == deckId, cancellationToken);
    }

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

    public async Task<UserFlashcardProgress?> GetCardProgressAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserFlashcardProgress
            .FirstOrDefaultAsync(
                progress => progress.UserId == userId && progress.CardId == cardId,
                cancellationToken);
    }

    public Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken)
    {
        dbContext.FlashcardDecks.Add(deck);
        return Task.CompletedTask;
    }

    public Task AddCardAsync(FlashcardCard card, CancellationToken cancellationToken)
    {
        dbContext.FlashcardCards.Add(card);
        return Task.CompletedTask;
    }

    public void RemoveDeck(FlashcardDeck deck)
    {
        dbContext.FlashcardDecks.Remove(deck);
    }

    public Task AddProgressAsync(UserFlashcardProgress progress, CancellationToken cancellationToken)
    {
        dbContext.UserFlashcardProgress.Add(progress);
        return Task.CompletedTask;
    }

    public async Task DeleteDeckProgressAsync(
        Guid userId,
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var cardIds = await dbContext.FlashcardCards
            .AsNoTracking()
            .Where(card => card.DeckId == deckId)
            .Select(card => card.Id)
            .ToListAsync(cancellationToken);

        if (cardIds.Count == 0)
            return;

        var rows = await dbContext.UserFlashcardProgress
            .Where(progress => progress.UserId == userId && cardIds.Contains(progress.CardId))
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
            dbContext.UserFlashcardProgress.RemoveRange(rows);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
