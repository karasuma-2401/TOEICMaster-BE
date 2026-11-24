using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Flashcards;

public sealed record FlashcardDeckAddCardResult(
    string DeckSlug,
    FlashcardCard Card);

public interface IFlashcardRepository
{
    Task<IReadOnlyList<FlashcardDeck>> GetVisibleDecksAsync(Guid? userId, CancellationToken cancellationToken);
    Task<FlashcardDeck?> GetVisibleDeckBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken);
    Task<bool> DeckVisibleToUserAsync(Guid userId, Guid deckId, CancellationToken cancellationToken);
    Task<bool> CardExistsVisibleToUserAsync(Guid cardId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFlashcardProgress>> GetUserProgressAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFlashcardMatchRecord>> GetUserMatchRecordsAsync(Guid userId, CancellationToken cancellationToken);
    Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken);
    Task<bool> DeleteOwnedDeckAsync(Guid userId, Guid deckId, CancellationToken cancellationToken);
    Task<FlashcardDeckAddCardResult?> AddCardToOwnedDeckAsync(
        Guid userId,
        Guid deckId,
        FlashcardCardInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<UserFlashcardProgress> UpsertProgressAtomicAsync(
        Guid userId,
        Guid cardId,
        string status,
        bool starred,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<(UserFlashcardMatchRecord Record, bool IsNewRecord)> RecordMatchAtomicAsync(
        Guid userId,
        Guid deckId,
        int elapsedMs,
        int maxCombo,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task DeleteDeckProgressAsync(Guid userId, Guid deckId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
