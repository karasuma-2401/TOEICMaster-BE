using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Flashcards;

internal static class FlashcardMapper
{
    public static FlashcardDeckDto ToDto(FlashcardDeck deck) =>
        new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Description,
            deck.Category,
            deck.TargetScore,
            deck.EstimatedMinutes,
            deck.IsCustom,
            deck.Cards
                .OrderBy(card => card.Ordinal)
                .Select(card => new FlashcardCardDto(
                    card.Id,
                    card.Term,
                    card.Phonetic,
                    card.PartOfSpeech,
                    card.MeaningVi,
                    card.ExampleEn,
                    card.ExampleVi,
                    card.GetCollocations(),
                    card.ToeicPart,
                    card.Level))
                .ToArray());

    public static CardProgressDto ToDto(UserFlashcardProgress progress) =>
        new(
            progress.Status,
            progress.Starred,
            progress.ReviewCount,
            progress.UpdatedAtUtc);
}

public sealed class GetFlashcardWorkspaceQueryHandler(IFlashcardRepository repository)
    : IRequestHandler<GetFlashcardWorkspaceQuery, FlashcardWorkspaceDto>
{
    public async Task<FlashcardWorkspaceDto> Handle(
        GetFlashcardWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var decks = await repository.GetVisibleDecksAsync(request.UserId, cancellationToken);
        var progressDict = new Dictionary<string, CardProgressDto>(StringComparer.OrdinalIgnoreCase);

        if (request.UserId.HasValue)
        {
            var records = await repository.GetUserProgressAsync(request.UserId.Value, cancellationToken);
            foreach (var record in records)
            {
                progressDict[record.CardId.ToString()] = FlashcardMapper.ToDto(record);
            }
        }

        var orderedDecks = decks
            .OrderByDescending(deck => deck.IsCustom)
            .ThenByDescending(deck => deck.IsCustom ? deck.CreatedAtUtc : DateTimeOffset.MinValue)
            .ThenBy(deck => deck.Slug, StringComparer.Ordinal)
            .Select(FlashcardMapper.ToDto)
            .ToArray();

        return new FlashcardWorkspaceDto(orderedDecks, progressDict);
    }
}

public sealed class GetFlashcardDeckBySlugQueryHandler(IFlashcardRepository repository)
    : IRequestHandler<GetFlashcardDeckBySlugQuery, FlashcardDeckDto?>
{
    public async Task<FlashcardDeckDto?> Handle(
        GetFlashcardDeckBySlugQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return null;

        var deck = await repository.GetVisibleDeckBySlugAsync(
            request.Slug.Trim().ToLowerInvariant(),
            request.UserId,
            cancellationToken);

        return deck is null ? null : FlashcardMapper.ToDto(deck);
    }
}

public sealed class CreateFlashcardDeckCommandHandler(
    IFlashcardRepository repository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateFlashcardDeckCommand, FlashcardDeckDto>
{
    public async Task<FlashcardDeckDto> Handle(
        CreateFlashcardDeckCommand command,
        CancellationToken cancellationToken)
    {
        var inputs = command.Request.Cards
            .Select(card => new FlashcardCardInput(
                card.Term,
                card.Phonetic,
                card.PartOfSpeech,
                card.MeaningVi,
                card.ExampleEn,
                card.ExampleVi,
                card.Collocations,
                card.ToeicPart,
                card.Level))
            .ToArray();

        var deck = FlashcardDeck.CreateCustom(
            command.UserId,
            command.Request.Title,
            command.Request.Description,
            inputs,
            timeProvider.GetUtcNow());

        await repository.AddDeckAsync(deck, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return FlashcardMapper.ToDto(deck);
    }
}

public sealed class DeleteFlashcardDeckCommandHandler(IFlashcardRepository repository)
    : IRequestHandler<DeleteFlashcardDeckCommand, bool>
{
    public async Task<bool> Handle(
        DeleteFlashcardDeckCommand command,
        CancellationToken cancellationToken)
    {
        var deck = await repository.GetDeckByIdAsync(command.DeckId, cancellationToken);
        if (deck is null || deck.OwnerUserId != command.UserId)
            return false;

        repository.RemoveDeck(deck);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class UpsertFlashcardProgressCommandHandler(
    IFlashcardRepository repository,
    TimeProvider timeProvider)
    : IRequestHandler<UpsertFlashcardProgressCommand, CardProgressDto?>
{
    public async Task<CardProgressDto?> Handle(
        UpsertFlashcardProgressCommand command,
        CancellationToken cancellationToken)
    {
        var exists = await repository.CardExistsVisibleToUserAsync(
            command.CardId,
            command.UserId,
            cancellationToken);

        if (!exists)
            return null;

        var now = timeProvider.GetUtcNow();
        var existing = await repository.GetCardProgressAsync(
            command.UserId,
            command.CardId,
            cancellationToken);

        if (existing is null)
        {
            existing = UserFlashcardProgress.Create(
                command.UserId,
                command.CardId,
                command.Request.Status,
                command.Request.Starred,
                now);
            await repository.AddProgressAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(command.Request.Status, command.Request.Starred, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return FlashcardMapper.ToDto(existing);
    }
}

public sealed class ResetFlashcardDeckProgressCommandHandler(IFlashcardRepository repository)
    : IRequestHandler<ResetFlashcardDeckProgressCommand, bool>
{
    public async Task<bool> Handle(
        ResetFlashcardDeckProgressCommand command,
        CancellationToken cancellationToken)
    {
        var deck = await repository.GetDeckByIdAsync(command.DeckId, cancellationToken);
        if (deck is null || (deck.OwnerUserId.HasValue && deck.OwnerUserId != command.UserId))
            return false;

        await repository.DeleteDeckProgressAsync(command.UserId, command.DeckId, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
