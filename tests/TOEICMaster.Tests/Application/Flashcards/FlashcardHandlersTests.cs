using TOEICMaster.Application.Flashcards;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Flashcards;

public sealed class FlashcardHandlersTests
{
    private sealed class FakeFlashcardRepository : IFlashcardRepository
    {
        public List<FlashcardDeck> Decks { get; } = [];
        public List<UserFlashcardProgress> Progress { get; } = [];

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

        public Task<FlashcardDeck?> GetDeckByIdAsync(Guid deckId, CancellationToken cancellationToken) =>
            Task.FromResult(Decks.FirstOrDefault(item => item.Id == deckId));

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

        public Task<UserFlashcardProgress?> GetCardProgressAsync(Guid userId, Guid cardId, CancellationToken cancellationToken) =>
            Task.FromResult(Progress.FirstOrDefault(item => item.UserId == userId && item.CardId == cardId));

        public Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken)
        {
            Decks.Add(deck);
            return Task.CompletedTask;
        }

        public Task AddCardAsync(FlashcardCard card, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void RemoveDeck(FlashcardDeck deck) => Decks.Remove(deck);

        public Task AddProgressAsync(UserFlashcardProgress progress, CancellationToken cancellationToken)
        {
            Progress.Add(progress);
            return Task.CompletedTask;
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
                    "Từ vựng Part 7",
                    "Ghi chú ôn thi",
                    [new CreateFlashcardCardRequest("reimburse", "/ˌriː.ɪmˈbɜːrs/", "verb", "hoàn trả chi phí", "Keep receipts.", "Giữ hóa đơn.", ["reimburse expenses"], "Part 7", "B2")])),
            CancellationToken.None);

        Assert.True(created.IsCustom);
        Assert.Equal("custom", created.Category);
        Assert.Single(created.Cards);
        Assert.StartsWith("tu-vung-part-7-", created.Slug);

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
            "Văn phòng",
            "Mô tả",
            FlashcardCategories.Office,
            "500+",
            5,
            [(builtInCardId, new FlashcardCardInput("agenda", null, "noun", "chương trình họp", null, null, null, "Part 3", "B1"))],
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
    public void Validators_reject_invalid_deck_and_progress_status()
    {
        var deckValidator = new CreateFlashcardDeckCommandValidator();
        var invalidDeck = deckValidator.Validate(new CreateFlashcardDeckCommand(
            Guid.NewGuid(),
            new CreateFlashcardDeckRequest("", null, [])));
        Assert.False(invalidDeck.IsValid);

        var progressValidator = new UpsertFlashcardProgressCommandValidator();
        var invalidProgress = progressValidator.Validate(new UpsertFlashcardProgressCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpsertFlashcardProgressRequest("unknown-status", false)));
        Assert.False(invalidProgress.IsValid);
    }

    [Fact]
    public async Task Add_card_appends_to_owned_custom_deck_and_rejects_other_users()
    {
        var repo = new FakeFlashcardRepository();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var createHandler = new CreateFlashcardDeckCommandHandler(repo, TimeProvider.System);
        var deck = await createHandler.Handle(
            new CreateFlashcardDeckCommand(
                ownerId,
                new CreateFlashcardDeckRequest(
                    "Từ vựng riêng",
                    null,
                    [new CreateFlashcardCardRequest("agenda", null, "noun", "chương trình họp", null, null, null, null, "B1")])),
            CancellationToken.None);

        var addHandler = new AddFlashcardCardCommandHandler(repo, TimeProvider.System);
        var forbidden = await addHandler.Handle(
            new AddFlashcardCardCommand(
                otherUserId,
                deck.Id,
                new CreateFlashcardCardRequest("invoice", null, "noun", "hóa đơn", null, null, null, null, "B1")),
            CancellationToken.None);
        Assert.Null(forbidden);

        var added = await addHandler.Handle(
            new AddFlashcardCardCommand(
                ownerId,
                deck.Id,
                new CreateFlashcardCardRequest("invoice", "/ˈɪn.vɔɪs/", "noun", "hóa đơn", "Check the invoice.", "Kiểm tra hóa đơn.", ["issue an invoice"], "Part 7", "B2")),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal("invoice", added.Term);
        Assert.Equal(2, repo.Decks[0].Cards.Count);
    }
}
