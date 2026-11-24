using System.Data.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence.Flashcards;

namespace TOEICMaster.Tests.Api.Flashcards;

public sealed class EfFlashcardRepositoryPostgresTests(FlashcardsApiFactory factory)
    : FlashcardsPostgresTestBase(factory), IClassFixture<FlashcardsApiFactory>
{
    [Fact]
    public async Task Add_card_persists_the_owned_deck_card_and_ordinal()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);

        var added = await repository.AddCardToOwnedDeckAsync(userId, deck.Id, CardInput("  negotiate  "),
            Now.AddMinutes(1), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(deck.Slug, added.DeckSlug);
        Assert.Equal(deck.Id, added.Card.DeckId);
        Assert.Equal(2, added.Card.Ordinal);
        Assert.Equal("negotiate", added.Card.Term);
        Assert.Equal("đàm phán", added.Card.MeaningVi);
        await using var verifyDb = CreateDbContext();
        var saved = await verifyDb.FlashcardCards.SingleAsync(card => card.Id == added.Card.Id);
        Assert.Equal(added.Card.Term, saved.Term);
        Assert.Equal(2, await verifyDb.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("built-in")]
    [InlineData("missing")]
    public async Task Add_card_returns_not_found_without_modifying_an_unowned_deck(string target)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(target == "built-in" ? null : ownerId);
        var deckId = target == "missing" ? Guid.NewGuid() : deck.Id;
        await using var db = CreateDbContext();

        var added = await new EfFlashcardRepository(db).AddCardToOwnedDeckAsync(userId, deckId, CardInput(),
            Now, CancellationToken.None);

        Assert.Null(added);
        Assert.Equal(1, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Fact]
    public async Task Add_card_at_the_200_card_limit_returns_a_validation_failure_without_a_write()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId, FlashcardDeck.MaxCardsPerDeck);
        await using var db = CreateDbContext();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            new EfFlashcardRepository(db).AddCardToOwnedDeckAsync(userId, deck.Id, CardInput(), Now,
                CancellationToken.None));

        var failure = Assert.Single(exception.Errors);
        Assert.Equal("Cards", failure.PropertyName);
        Assert.Contains("200", failure.ErrorMessage);
        Assert.Equal(200, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Fact]
    public async Task Concurrent_card_adds_serialize_at_the_cap_and_keep_ordinals_unique()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId, FlashcardDeck.MaxCardsPerDeck - 1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> AddAsync(int index)
        {
            await using var db = CreateDbContext();
            await start.Task;
            try
            {
                var added = await new EfFlashcardRepository(db).AddCardToOwnedDeckAsync(userId, deck.Id,
                    CardInput($"concurrent-{index}"), Now, CancellationToken.None);
                Assert.NotNull(added);
                return true;
            }
            catch (ValidationException exception)
            {
                Assert.Equal("Cards", Assert.Single(exception.Errors).PropertyName);
                return false;
            }
        }

        var submissions = Enumerable.Range(1, 8).Select(AddAsync).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(submissions);

        Assert.Single(results, added => added);
        Assert.Equal(7, results.Count(added => !added));
        await using var verifyDb = CreateDbContext();
        var ordinals = await verifyDb.FlashcardCards.Where(card => card.DeckId == deck.Id)
            .OrderBy(card => card.Ordinal).Select(card => card.Ordinal).ToArrayAsync();
        Assert.Equal(Enumerable.Range(1, 200), ordinals);
    }

    [Theory]
    [InlineData(FlashcardMasteryStatus.New, 0)]
    [InlineData(FlashcardMasteryStatus.Learning, 1)]
    [InlineData(FlashcardMasteryStatus.Mastered, 1)]
    public async Task Initial_progress_has_the_expected_review_count(string status, int expectedCount)
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null);
        var cardId = deck.Cards.Single().Id;
        await using var db = CreateDbContext();

        var saved = await new EfFlashcardRepository(db).UpsertProgressAtomicAsync(userId, cardId, status, true,
            Now, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(cardId, saved.CardId);
        Assert.Equal(status, saved.Status);
        Assert.True(saved.Starred);
        Assert.Equal(expectedCount, saved.ReviewCount);
        Assert.Equal(Now, saved.UpdatedAtUtc);
        var persisted = await db.UserFlashcardProgress.SingleAsync(progress => progress.UserId == userId);
        Assert.Equal(saved.Id, persisted.Id);
        Assert.Equal(expectedCount, persisted.ReviewCount);
    }

    [Fact]
    public async Task Progress_reviews_increment_but_star_only_toggles_and_new_status_do_not()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var cardId = deck.Cards.Single().Id;
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);
        var updates = new[]
        {
            (FlashcardMasteryStatus.Learning, false, 1),
            (FlashcardMasteryStatus.Learning, true, 1),
            (FlashcardMasteryStatus.Review, true, 2),
            (FlashcardMasteryStatus.Review, true, 3),
            (FlashcardMasteryStatus.Review, false, 3),
            (FlashcardMasteryStatus.New, false, 3),
            (FlashcardMasteryStatus.Mastered, true, 4)
        };
        Guid? progressId = null;
        for (var index = 0; index < updates.Length; index++)
        {
            var (status, starred, count) = updates[index];
            var saved = await repository.UpsertProgressAtomicAsync(userId, cardId, status, starred,
                Now.AddMinutes(index), CancellationToken.None);
            Assert.NotNull(saved);
            progressId ??= saved.Id;
            Assert.Equal(progressId.Value, saved.Id);
            Assert.Equal(count, saved.ReviewCount);
            Assert.Equal(status, saved.Status);
            Assert.Equal(starred, saved.Starred);
            Assert.Equal(Now.AddMinutes(index), saved.UpdatedAtUtc);
        }

        var persisted = await db.UserFlashcardProgress.SingleAsync(progress => progress.UserId == userId);
        Assert.Equal(4, persisted.ReviewCount);
        Assert.Equal(FlashcardMasteryStatus.Mastered, persisted.Status);
    }

    [Fact]
    public async Task Concurrent_progress_upserts_keep_one_row_and_count_every_review()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var cardId = deck.Cards.Single().Id;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Guid> ReviewAsync()
        {
            await using var db = CreateDbContext();
            await start.Task;
            var saved = await new EfFlashcardRepository(db).UpsertProgressAtomicAsync(userId, cardId,
                FlashcardMasteryStatus.Learning, false, Now, CancellationToken.None);
            Assert.NotNull(saved);
            return saved.Id;
        }

        var submissions = Enumerable.Range(1, 8).Select(_ => ReviewAsync()).ToArray();
        start.SetResult();
        var ids = await Task.WhenAll(submissions);

        Assert.Single(ids.Distinct());
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.UserFlashcardProgress.SingleAsync(progress => progress.UserId == userId);
        Assert.Equal(8, persisted.ReviewCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Progress_for_a_missing_or_deleted_card_returns_null(bool deleteDeck)
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var cardId = deleteDeck ? deck.Cards.Single().Id : Guid.NewGuid();
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);
        if (deleteDeck)
            Assert.True(await repository.DeleteOwnedDeckAsync(userId, deck.Id, CancellationToken.None));

        var saved = await repository.UpsertProgressAtomicAsync(userId, cardId, FlashcardMasteryStatus.Learning,
            false, Now, CancellationToken.None);

        Assert.Null(saved);
        Assert.False(await db.UserFlashcardProgress.AnyAsync(progress => progress.UserId == userId));
    }

    [Fact]
    public async Task Match_records_distinguish_first_faster_slower_and_equal_times()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);
        var updates = new[]
        {
            (18_000, 3, 18_000, 3, true),
            (22_000, 8, 18_000, 8, false),
            (18_000, 6, 18_000, 8, false),
            (12_000, 4, 12_000, 8, true)
        };
        Guid? recordId = null;
        for (var index = 0; index < updates.Length; index++)
        {
            var (elapsedMs, combo, bestTime, bestCombo, expectedNewRecord) = updates[index];
            var (record, isNewRecord) = await repository.RecordMatchAtomicAsync(userId, deck.Id, elapsedMs,
                combo, Now.AddMinutes(index), CancellationToken.None);
            recordId ??= record.Id;
            Assert.Equal(recordId.Value, record.Id);
            Assert.Equal(expectedNewRecord, isNewRecord);
            Assert.Equal(bestTime, record.BestTimeMs);
            Assert.Equal(bestCombo, record.BestCombo);
            Assert.Equal(index + 1, record.PlayCount);
            Assert.Equal(Now.AddMinutes(index), record.UpdatedAtUtc);
        }

        var persisted = await db.UserFlashcardMatchRecords.SingleAsync(record => record.UserId == userId);
        Assert.Equal(12_000, persisted.BestTimeMs);
        Assert.Equal(8, persisted.BestCombo);
        Assert.Equal(4, persisted.PlayCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_slower_concurrent_match_cannot_claim_a_record_after_a_faster_match_commits(bool existingRecord)
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        if (existingRecord)
            await SeedMatchAsync(userId, deck.Id);

        var fasterReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fasterCommitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fasterDb = CreateDbContext(new MatchSubmissionInterceptor(true, fasterReady, slowerReady, fasterCommitted));
        await using var slowerDb = CreateDbContext(new MatchSubmissionInterceptor(false, fasterReady, slowerReady, fasterCommitted));
        var fasterTask = new EfFlashcardRepository(fasterDb).RecordMatchAtomicAsync(userId, deck.Id, 12_000, 4,
            Now, CancellationToken.None);
        await fasterReady.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var slowerTask = new EfFlashcardRepository(slowerDb).RecordMatchAtomicAsync(userId, deck.Id, 15_000, 8,
            Now, CancellationToken.None);

        var faster = await fasterTask.WaitAsync(TimeSpan.FromSeconds(15));
        fasterCommitted.SetResult();
        var slower = await slowerTask.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(faster.IsNewRecord);
        Assert.False(slower.IsNewRecord);
        Assert.Equal(12_000, slower.Record.BestTimeMs);
        Assert.Equal(8, slower.Record.BestCombo);
        Assert.Equal(existingRecord ? 3 : 2, slower.Record.PlayCount);
        var persisted = await slowerDb.UserFlashcardMatchRecords.SingleAsync(record => record.UserId == userId);
        Assert.Equal(slower.Record.PlayCount, persisted.PlayCount);
    }

    [Fact]
    public async Task Concurrent_equal_matches_report_exactly_one_first_record_and_keep_every_play()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> MatchAsync(int combo)
        {
            await using var db = CreateDbContext();
            await start.Task;
            var result = await new EfFlashcardRepository(db).RecordMatchAtomicAsync(userId, deck.Id, 12_000,
                combo, Now, CancellationToken.None);
            return result.IsNewRecord;
        }

        var submissions = Enumerable.Range(1, 8).Select(MatchAsync).ToArray();
        start.SetResult();
        var newRecords = await Task.WhenAll(submissions);

        Assert.Single(newRecords, isNewRecord => isNewRecord);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.UserFlashcardMatchRecords.SingleAsync(record => record.UserId == userId);
        Assert.Equal(12_000, persisted.BestTimeMs);
        Assert.Equal(8, persisted.BestCombo);
        Assert.Equal(8, persisted.PlayCount);
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("built-in")]
    [InlineData("missing")]
    public async Task Delete_deck_cannot_remove_an_unowned_or_missing_deck(string target)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(target == "built-in" ? null : ownerId);
        await using var db = CreateDbContext();

        var deleted = await new EfFlashcardRepository(db).DeleteOwnedDeckAsync(userId,
            target == "missing" ? Guid.NewGuid() : deck.Id, CancellationToken.None);

        Assert.False(deleted);
        Assert.True(await db.FlashcardDecks.AnyAsync(item => item.Id == deck.Id));
        Assert.Equal(1, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Fact]
    public async Task Delete_owned_deck_cascades_cards_progress_and_records_but_preserves_other_decks()
    {
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId, 2);
        var otherDeck = await CreateDeckAsync(otherUserId);
        foreach (var card in deck.Cards)
            await SeedProgressAsync(userId, card.Id);
        await SeedMatchAsync(userId, deck.Id);
        await SeedProgressAsync(otherUserId, otherDeck.Cards.Single().Id);
        await SeedMatchAsync(otherUserId, otherDeck.Id);
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);

        Assert.True(await repository.DeleteOwnedDeckAsync(userId, deck.Id, CancellationToken.None));
        Assert.False(await repository.DeleteOwnedDeckAsync(userId, deck.Id, CancellationToken.None));
        Assert.False(await db.FlashcardDecks.AnyAsync(item => item.Id == deck.Id));
        Assert.Equal(0, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
        Assert.Equal(0, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == userId));
        Assert.Equal(0, await db.UserFlashcardMatchRecords.CountAsync(record => record.DeckId == deck.Id));
        Assert.True(await db.FlashcardDecks.AnyAsync(item => item.Id == otherDeck.Id));
        Assert.Equal(1, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == otherUserId));
        Assert.Equal(1, await db.UserFlashcardMatchRecords.CountAsync(record => record.DeckId == otherDeck.Id));
    }

    [Fact]
    public async Task Delete_deck_progress_is_scoped_to_the_user_and_deck_and_keeps_match_records()
    {
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null, 2);
        var otherDeck = await CreateDeckAsync(userId);
        foreach (var card in deck.Cards)
        {
            await SeedProgressAsync(userId, card.Id);
            await SeedProgressAsync(otherUserId, card.Id);
        }
        await SeedProgressAsync(userId, otherDeck.Cards.Single().Id);
        await SeedMatchAsync(userId, deck.Id);
        await using var db = CreateDbContext();
        var repository = new EfFlashcardRepository(db);

        await repository.DeleteDeckProgressAsync(userId, deck.Id, CancellationToken.None);
        await repository.DeleteDeckProgressAsync(userId, deck.Id, CancellationToken.None);

        Assert.Equal(1, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == userId));
        Assert.Equal(otherDeck.Cards.Single().Id,
            (await db.UserFlashcardProgress.SingleAsync(progress => progress.UserId == userId)).CardId);
        Assert.Equal(2, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == otherUserId));
        Assert.Equal(1, await db.UserFlashcardMatchRecords.CountAsync(record => record.UserId == userId));
        Assert.Equal(2, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    private sealed class MatchSubmissionInterceptor(
        bool faster,
        TaskCompletionSource fasterReady,
        TaskCompletionSource slowerReady,
        TaskCompletionSource fasterCommitted) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!faster && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
                slowerReady.TrySetResult();
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!faster && command.CommandText.Contains("user_flashcard_match_records", StringComparison.Ordinal)
                && !command.CommandText.Contains("INSERT", StringComparison.Ordinal))
                slowerReady.TrySetResult();
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO user_flashcard_match_records", StringComparison.Ordinal))
            {
                if (faster)
                    fasterReady.TrySetResult();
                var ready = faster ? slowerReady.Task : fasterCommitted.Task;
                await ready.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
