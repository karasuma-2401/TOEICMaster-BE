using FluentValidation;
using TOEICMaster.Application.Flashcards;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Flashcards;

public sealed class FlashcardHandlersTests
{
    private sealed class FakeFlashcardRepository : IFlashcardRepository
    {
        public List<FlashcardDeck> Decks { get; } = [];
        public List<UserFlashcardProgress> Progress { get; } = [];
        public List<UserFlashcardMatchRecord> MatchRecords { get; } = [];

        public Task<IReadOnlyList<FlashcardDeck>> GetVisibleDecksAsync(Guid? userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<FlashcardDeck> result = Decks
                .Where(deck => deck.OwnerUserId is null || (userId.HasValue && deck.OwnerUserId == userId.Value))
                .ToList();
            return Task.FromResult(result);
        }

        public Task<FlashcardDeck?> GetVisibleDeckBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken)
        {
            var deck = Decks.FirstOrDefault(item =>
                item.Slug == slug && (item.OwnerUserId is null || (userId.HasValue && item.OwnerUserId == userId.Value)));
            return Task.FromResult(deck);
        }

        public Task<bool> DeckVisibleToUserAsync(Guid userId, Guid deckId, CancellationToken cancellationToken)
        {
            var visible = Decks.Any(deck => deck.Id == deckId && (deck.OwnerUserId is null || deck.OwnerUserId == userId));
            return Task.FromResult(visible);
        }

        public Task<bool> CardExistsVisibleToUserAsync(Guid cardId, Guid userId, CancellationToken cancellationToken)
        {
            var exists = Decks
                .Where(deck => deck.OwnerUserId is null || deck.OwnerUserId == userId)
                .SelectMany(deck => deck.Cards)
                .Any(card => card.Id == cardId);
            return Task.FromResult(exists);
        }

        public Task<IReadOnlyList<UserFlashcardProgress>> GetUserProgressAsync(Guid userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<UserFlashcardProgress> result = Progress.Where(item => item.UserId == userId).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<UserFlashcardMatchRecord>> GetUserMatchRecordsAsync(Guid userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<UserFlashcardMatchRecord> result = MatchRecords.Where(r => r.UserId == userId).ToList();
            return Task.FromResult(result);
        }

        public Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken)
        {
            Decks.Add(deck);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteOwnedDeckAsync(Guid userId, Guid deckId, CancellationToken cancellationToken)
        {
            var removed = Decks.RemoveAll(deck => deck.Id == deckId && deck.OwnerUserId == userId);
            return Task.FromResult(removed > 0);
        }

        public Task<FlashcardDeckAddCardResult?> AddCardToOwnedDeckAsync(
            Guid userId,
            Guid deckId,
            FlashcardCardInput input,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            var deck = Decks.FirstOrDefault(item => item.Id == deckId && item.OwnerUserId == userId);
            if (deck is null)
            {
                return Task.FromResult<FlashcardDeckAddCardResult?>(null);
            }

            if (deck.Cards.Count >= FlashcardDeck.MaxCardsPerDeck)
            {
                throw new ValidationException(
                    $"A deck can contain at most {FlashcardDeck.MaxCardsPerDeck} cards.");
            }

            var card = deck.AddCard(input, nowUtc);
            return Task.FromResult<FlashcardDeckAddCardResult?>(new FlashcardDeckAddCardResult(deck.Slug, card));
        }

        public Task<UserFlashcardProgress> UpsertProgressAtomicAsync(
            Guid userId,
            Guid cardId,
            string status,
            bool starred,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            var existing = Progress.FirstOrDefault(item => item.UserId == userId && item.CardId == cardId);
            if (existing is null)
            {
                var created = UserFlashcardProgress.Create(userId, cardId, status, starred, nowUtc);
                Progress.Add(created);
                return Task.FromResult(created);
            }

            existing.Update(status, starred, nowUtc);
            return Task.FromResult(existing);
        }

        public Task<(UserFlashcardMatchRecord Record, bool IsNewRecord)> RecordMatchAtomicAsync(
            Guid userId,
            Guid deckId,
            int elapsedMs,
            int maxCombo,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            var existing = MatchRecords.FirstOrDefault(item => item.UserId == userId && item.DeckId == deckId);
            if (existing is null)
            {
                var created = UserFlashcardMatchRecord.Create(userId, deckId, elapsedMs, maxCombo, nowUtc);
                MatchRecords.Add(created);
                return Task.FromResult((created, true));
            }

            var isNewRecord = existing.RecordRun(elapsedMs, maxCombo, nowUtc);
            return Task.FromResult((existing, isNewRecord));
        }

        public Task DeleteDeckProgressAsync(Guid userId, Guid deckId, CancellationToken cancellationToken)
        {
            var cardIds = Decks.Where(d => d.Id == deckId).SelectMany(d => d.Cards).Select(c => c.Id).ToHashSet();
            Progress.RemoveAll(p => p.UserId == userId && cardIds.Contains(p.CardId));
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Create_and_delete_custom_deck_enforces_ownership()
    {
        var repo = new FakeFlashcardRepository();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var createHandler = new CreateFlashcardDeckCommandHandler(repo, TimeProvider.System);
        var created = await createHandler.Handle(
            new CreateFlashcardDeckCommand(
                ownerId,
                new CreateFlashcardDeckRequest(
                    "Part 7 Vocabulary",
                    "Exam study notes",
                    [new CreateFlashcardCardRequest("reimburse", "/ˌriː.ɪmˈbɜːrs/", "verb", "reimburse expenses", "Keep receipts.", "Keep receipts.", ["reimburse expenses"], "Part 7", "B2")])),
            CancellationToken.None);

        Assert.True(created.IsCustom);
        Assert.Equal("custom", created.Category);
        Assert.Single(created.Cards);
        Assert.StartsWith("part-7-vocabulary-", created.Slug);

        var deleteHandler = new DeleteFlashcardDeckCommandHandler(repo);
        Assert.False(await deleteHandler.Handle(new DeleteFlashcardDeckCommand(otherUserId, created.Id), CancellationToken.None));
        Assert.True(await deleteHandler.Handle(new DeleteFlashcardDeckCommand(ownerId, created.Id), CancellationToken.None));
        Assert.Empty(repo.Decks);
    }

    [Fact]
    public async Task Upsert_and_reset_progress_tracks_review_count_and_visibility()
    {
        var repo = new FakeFlashcardRepository();
        var userId = Guid.NewGuid();
        var builtInCardId = Guid.NewGuid();
        var builtInDeck = FlashcardDeck.CreateBuiltIn(
            Guid.NewGuid(),
            "office-operations",
            "Office",
            "Description",
            FlashcardCategories.Office,
            "500+",
            5,
            [(builtInCardId, new FlashcardCardInput("agenda", null, "noun", "meeting agenda", null, null, null, "Part 3", "B1"))],
            DateTimeOffset.UtcNow);
        repo.Decks.Add(builtInDeck);

        var upsertHandler = new UpsertFlashcardProgressCommandHandler(repo, TimeProvider.System);
        var first = await upsertHandler.Handle(
            new UpsertFlashcardProgressCommand(userId, builtInCardId, new UpsertFlashcardProgressRequest("review", true)),
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("review", first.Status);
        Assert.True(first.Starred);
        Assert.Equal(1, first.ReviewCount);

        var second = await upsertHandler.Handle(
            new UpsertFlashcardProgressCommand(userId, builtInCardId, new UpsertFlashcardProgressRequest("mastered", true)),
            CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal("mastered", second.Status);
        Assert.Equal(2, second.ReviewCount);

        var workspaceHandler = new GetFlashcardWorkspaceQueryHandler(repo);
        var workspace = await workspaceHandler.Handle(new GetFlashcardWorkspaceQuery(userId), CancellationToken.None);
        Assert.Single(workspace.Decks);
        Assert.True(workspace.Progress.ContainsKey(builtInCardId.ToString()));

        var resetHandler = new ResetFlashcardDeckProgressCommandHandler(repo);
        Assert.True(await resetHandler.Handle(new ResetFlashcardDeckProgressCommand(userId, builtInDeck.Id), CancellationToken.None));
        Assert.Empty(repo.Progress);
    }

    [Fact]
    public void Validators_reject_invalid_deck_card_fields_and_progress_status()
    {
        var deckValidator = new CreateFlashcardDeckCommandValidator();
        var invalidDeck = deckValidator.Validate(new CreateFlashcardDeckCommand(
            Guid.NewGuid(),
            new CreateFlashcardDeckRequest("", null, [])));
        Assert.False(invalidDeck.IsValid);

        var addCardValidator = new AddFlashcardCardCommandValidator();
        var invalidLevel = addCardValidator.Validate(new AddFlashcardCardCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CreateFlashcardCardRequest("agenda", null, "noun", "meeting agenda", null, null, null, "Part 3", "Z9")));
        Assert.False(invalidLevel.IsValid);

        var oversizedToeicPart = addCardValidator.Validate(new AddFlashcardCardCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CreateFlashcardCardRequest("agenda", null, "noun", "meeting agenda", null, null, null, new string('p', 65), "B1")));
        Assert.False(oversizedToeicPart.IsValid);

        var tooManyCollocations = addCardValidator.Validate(new AddFlashcardCardCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CreateFlashcardCardRequest(
                "agenda",
                null,
                "noun",
                "meeting agenda",
                null,
                null,
                Enumerable.Range(1, 11).Select(i => $"collocation {i}").ToArray(),
                "Part 3",
                "B1")));
        Assert.False(tooManyCollocations.IsValid);

        var progressValidator = new UpsertFlashcardProgressCommandValidator();
        var invalidProgress = progressValidator.Validate(new UpsertFlashcardProgressCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpsertFlashcardProgressRequest("unknown-status", false)));
        Assert.False(invalidProgress.IsValid);
    }

    [Fact]
    public async Task Add_card_appends_to_owned_custom_deck_and_rejects_other_users_and_over_cap()
    {
        var repo = new FakeFlashcardRepository();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var createHandler = new CreateFlashcardDeckCommandHandler(repo, TimeProvider.System);
        var deck = await createHandler.Handle(
            new CreateFlashcardDeckCommand(
                ownerId,
                new CreateFlashcardDeckRequest(
                    "Personal Vocabulary",
                    null,
                    [new CreateFlashcardCardRequest("agenda", null, "noun", "meeting agenda", null, null, null, null, "B1")])),
            CancellationToken.None);

        var addHandler = new AddFlashcardCardCommandHandler(repo, TimeProvider.System);
        var forbidden = await addHandler.Handle(
            new AddFlashcardCardCommand(
                otherUserId,
                deck.Id,
                new CreateFlashcardCardRequest("invoice", null, "noun", "invoice", null, null, null, null, "B1")),
            CancellationToken.None);
        Assert.Null(forbidden);

        var added = await addHandler.Handle(
            new AddFlashcardCardCommand(
                ownerId,
                deck.Id,
                new CreateFlashcardCardRequest("invoice", "/ˈɪn.vɔɪs/", "noun", "invoice", "Check the invoice.", "Check the invoice.", ["issue an invoice"], "Part 7", "B2")),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(deck.Slug, added.DeckSlug);
        Assert.Equal("invoice", added.Card.Term);
        Assert.Equal(2, repo.Decks[0].Cards.Count);

        var fullDeck = FlashcardDeck.CreateCustom(
            ownerId,
            "Full Deck",
            null,
            Enumerable.Range(1, FlashcardDeck.MaxCardsPerDeck)
                .Select(i => new FlashcardCardInput($"term-{i}", null, "noun", $"meaning-{i}", null, null, null, "Part 5", "B1"))
                .ToList(),
            DateTimeOffset.UtcNow);
        repo.Decks.Add(fullDeck);

        await Assert.ThrowsAsync<ValidationException>(() => addHandler.Handle(
            new AddFlashcardCardCommand(
                ownerId,
                fullDeck.Id,
                new CreateFlashcardCardRequest("overflow", null, "noun", "overflow", null, null, null, "Part 5", "B1")),
            CancellationToken.None));
    }

    [Fact]
    public async Task Record_match_tracks_fastest_time_and_best_combo()
    {
        var repo = new FakeFlashcardRepository();
        var userId = Guid.NewGuid();
        var deck = FlashcardDeck.CreateBuiltIn(
            Guid.NewGuid(),
            "office-operations",
            "Office",
            "Description",
            FlashcardCategories.Office,
            "500+",
            5,
            [(Guid.NewGuid(), new FlashcardCardInput("agenda", null, "noun", "meeting agenda", null, null, null, "Part 3", "B1"))],
            DateTimeOffset.UtcNow);
        repo.Decks.Add(deck);

        var handler = new RecordFlashcardMatchCommandHandler(repo, TimeProvider.System);
        var first = await handler.Handle(
            new RecordFlashcardMatchCommand(userId, deck.Id, new RecordFlashcardMatchRequest(18500, 4)),
            CancellationToken.None);
        Assert.NotNull(first);
        Assert.True(first.IsNewRecord);
        Assert.Equal(18500, first.BestTimeMs);
        Assert.Equal(1, first.PlayCount);

        var slower = await handler.Handle(
            new RecordFlashcardMatchCommand(userId, deck.Id, new RecordFlashcardMatchRequest(22000, 6)),
            CancellationToken.None);
        Assert.NotNull(slower);
        Assert.False(slower.IsNewRecord);
        Assert.Equal(18500, slower.BestTimeMs);
        Assert.Equal(6, slower.BestCombo);
        Assert.Equal(2, slower.PlayCount);

        var faster = await handler.Handle(
            new RecordFlashcardMatchCommand(userId, deck.Id, new RecordFlashcardMatchRequest(14200, 5)),
            CancellationToken.None);
        Assert.NotNull(faster);
        Assert.True(faster.IsNewRecord);
        Assert.Equal(14200, faster.BestTimeMs);
        Assert.Equal(6, faster.BestCombo);
        Assert.Equal(3, faster.PlayCount);
    }
}
