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
                .Select(ToDto)
                .ToArray());

    public static FlashcardCardDto ToDto(FlashcardCard card) =>
        new(
            card.Id,
            card.Term,
            card.Phonetic,
            card.PartOfSpeech,
            card.MeaningVi,
            card.ExampleEn,
            card.ExampleVi,
            card.GetCollocations(),
            card.ToeicPart,
            card.Level);

    public static CardProgressDto ToDto(UserFlashcardProgress progress) =>
        new(
            progress.Status,
            progress.Starred,
            progress.ReviewCount,
            progress.UpdatedAtUtc);

    public static MatchRecordDto ToDto(UserFlashcardMatchRecord record, bool isNewRecord = false) =>
        new(
            record.BestTimeMs,
            record.BestCombo,
            record.PlayCount,
            record.UpdatedAtUtc,
            isNewRecord);
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
        var matchDict = new Dictionary<string, MatchRecordDto>(StringComparer.OrdinalIgnoreCase);

        if (request.UserId.HasValue)
        {
            var records = await repository.GetUserProgressAsync(request.UserId.Value, cancellationToken);
            foreach (var record in records)
            {
                progressDict[record.CardId.ToString()] = FlashcardMapper.ToDto(record);
            }

            var matchRecords = await repository.GetUserMatchRecordsAsync(request.UserId.Value, cancellationToken);
            foreach (var match in matchRecords)
            {
                matchDict[match.DeckId.ToString()] = FlashcardMapper.ToDto(match);
            }
        }

        var orderedDecks = decks
            .OrderByDescending(deck => deck.IsCustom)
            .ThenByDescending(deck => deck.IsCustom ? deck.CreatedAtUtc : DateTimeOffset.MinValue)
            .ThenBy(deck => deck.Slug, StringComparer.Ordinal)
            .Select(FlashcardMapper.ToDto)
            .ToArray();

        return new FlashcardWorkspaceDto(orderedDecks, progressDict, matchDict);
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
    public Task<bool> Handle(
        DeleteFlashcardDeckCommand command,
        CancellationToken cancellationToken) =>
        repository.DeleteOwnedDeckAsync(command.UserId, command.DeckId, cancellationToken);
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

        var normalizedStatus = FlashcardMasteryStatus.Normalize(command.Request.Status);
        var saved = await repository.UpsertProgressAtomicAsync(
            command.UserId,
            command.CardId,
            normalizedStatus,
            command.Request.Starred,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return FlashcardMapper.ToDto(saved);
    }
}

public sealed class ResetFlashcardDeckProgressCommandHandler(IFlashcardRepository repository)
    : IRequestHandler<ResetFlashcardDeckProgressCommand, bool>
{
    public async Task<bool> Handle(
        ResetFlashcardDeckProgressCommand command,
        CancellationToken cancellationToken)
    {
        var visible = await repository.DeckVisibleToUserAsync(
            command.UserId,
            command.DeckId,
            cancellationToken);

        if (!visible)
            return false;

        await repository.DeleteDeckProgressAsync(command.UserId, command.DeckId, cancellationToken);
        return true;
    }
}

public sealed class AddFlashcardCardCommandHandler(
    IFlashcardRepository repository,
    TimeProvider timeProvider)
    : IRequestHandler<AddFlashcardCardCommand, AddFlashcardCardResult?>
{
    public async Task<AddFlashcardCardResult?> Handle(
        AddFlashcardCardCommand command,
        CancellationToken cancellationToken)
    {
        var input = new FlashcardCardInput(
            command.Request.Term,
            command.Request.Phonetic,
            command.Request.PartOfSpeech,
            command.Request.MeaningVi,
            command.Request.ExampleEn,
            command.Request.ExampleVi,
            command.Request.Collocations,
            command.Request.ToeicPart,
            command.Request.Level);

        var added = await repository.AddCardToOwnedDeckAsync(
            command.UserId,
            command.DeckId,
            input,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return added is null
            ? null
            : new AddFlashcardCardResult(added.DeckSlug, FlashcardMapper.ToDto(added.Card));
    }
}

public sealed class RecordFlashcardMatchCommandHandler(
    IFlashcardRepository repository,
    TimeProvider timeProvider)
    : IRequestHandler<RecordFlashcardMatchCommand, MatchRecordDto?>
{
    public async Task<MatchRecordDto?> Handle(
        RecordFlashcardMatchCommand command,
        CancellationToken cancellationToken)
    {
        var visible = await repository.DeckVisibleToUserAsync(
            command.UserId,
            command.DeckId,
            cancellationToken);

        if (!visible)
            return null;

        var (record, isNewRecord) = await repository.RecordMatchAtomicAsync(
            command.UserId,
            command.DeckId,
            command.Request.ElapsedMs,
            command.Request.MaxCombo,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return FlashcardMapper.ToDto(record, isNewRecord);
    }
}
