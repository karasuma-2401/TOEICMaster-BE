using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Flashcards;

public interface IFlashcardRepository
{
    Task<IReadOnlyList<FlashcardDeck>> GetVisibleDecksAsync(Guid? userId, CancellationToken cancellationToken);
    Task<FlashcardDeck?> GetVisibleDeckBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken);
    Task<FlashcardDeck?> GetDeckByIdAsync(Guid deckId, CancellationToken cancellationToken);
    Task<bool> CardExistsVisibleToUserAsync(Guid cardId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFlashcardProgress>> GetUserProgressAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserFlashcardProgress?> GetCardProgressAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);
    Task AddDeckAsync(FlashcardDeck deck, CancellationToken cancellationToken);
    void RemoveDeck(FlashcardDeck deck);
    Task AddProgressAsync(UserFlashcardProgress progress, CancellationToken cancellationToken);
    Task DeleteDeckProgressAsync(Guid userId, Guid deckId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
