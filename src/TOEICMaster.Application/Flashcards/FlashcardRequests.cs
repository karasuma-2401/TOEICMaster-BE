using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Flashcards;

public sealed record GetFlashcardWorkspaceQuery(Guid? UserId) : IRequest<FlashcardWorkspaceDto>;

public sealed record GetFlashcardDeckBySlugQuery(string Slug, Guid? UserId) : IRequest<FlashcardDeckDto?>;

public sealed record CreateFlashcardDeckCommand(
    Guid UserId,
    CreateFlashcardDeckRequest Request) : IRequest<FlashcardDeckDto>;

public sealed record DeleteFlashcardDeckCommand(
    Guid UserId,
    Guid DeckId) : IRequest<bool>;

public sealed record AddFlashcardCardResult(
    string DeckSlug,
    FlashcardCardDto Card);

public sealed record AddFlashcardCardCommand(
    Guid UserId,
    Guid DeckId,
    CreateFlashcardCardRequest Request) : IRequest<AddFlashcardCardResult?>;

public sealed record UpsertFlashcardProgressCommand(
    Guid UserId,
    Guid CardId,
    UpsertFlashcardProgressRequest Request) : IRequest<CardProgressDto?>;

public sealed record ResetFlashcardDeckProgressCommand(
    Guid UserId,
    Guid DeckId) : IRequest<bool>;

public sealed record RecordFlashcardMatchCommand(
    Guid UserId,
    Guid DeckId,
    RecordFlashcardMatchRequest Request) : IRequest<MatchRecordDto?>;

internal static class FlashcardCardValidationRules
{
    private static readonly IReadOnlySet<string> AllowedLevels =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "B1", "B2", "C1" };

    public static void Configure(InlineValidator<CreateFlashcardCardRequest> card)
    {
        card.RuleFor(c => c.Term)
            .NotEmpty().WithMessage("English term cannot be empty.")
            .MaximumLength(120);
        card.RuleFor(c => c.MeaningVi)
            .NotEmpty().WithMessage("Vietnamese meaning cannot be empty.")
            .MaximumLength(300);
        card.RuleFor(c => c.Phonetic)
            .MaximumLength(120)
            .When(c => c.Phonetic is not null);
        card.RuleFor(c => c.PartOfSpeech)
            .MaximumLength(40)
            .When(c => c.PartOfSpeech is not null);
        card.RuleFor(c => c.ExampleEn)
            .MaximumLength(400)
            .When(c => c.ExampleEn is not null);
        card.RuleFor(c => c.ExampleVi)
            .MaximumLength(400)
            .When(c => c.ExampleVi is not null);
        card.RuleFor(c => c.ToeicPart)
            .MaximumLength(64).WithMessage("TOEIC part label cannot exceed 64 characters.")
            .When(c => c.ToeicPart is not null);
        card.RuleFor(c => c.Level)
            .Must(level => string.IsNullOrWhiteSpace(level) || AllowedLevels.Contains(level.Trim()))
            .WithMessage("Level must be one of: B1, B2, C1.")
            .When(c => c.Level is not null);
        card.RuleFor(c => c.Collocations)
            .Must(collocations => collocations is null || collocations.Count <= 10)
            .WithMessage("A card can have at most 10 collocations.")
            .Must(collocations => collocations is null || collocations.All(item => item is not null && item.Trim().Length <= 80))
            .WithMessage("Each collocation cannot exceed 80 characters.")
            .Must(collocations =>
            {
                if (collocations is null) return true;
                var serialized = string.Join('|', collocations
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim().Replace("|", string.Empty)));
                return serialized.Length <= 500;
            })
            .WithMessage("Combined collocations cannot exceed 500 characters.")
            .When(c => c.Collocations is not null);
    }
}

public sealed class CreateFlashcardDeckCommandValidator : AbstractValidator<CreateFlashcardDeckCommand>
{
    public CreateFlashcardDeckCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Deck title cannot be empty.")
            .MaximumLength(120);
        RuleFor(x => x.Request.Description)
            .MaximumLength(400)
            .When(x => x.Request.Description is not null);
        RuleFor(x => x.Request.Cards)
            .NotEmpty().WithMessage("Deck must contain at least 1 vocabulary card.")
            .Must(cards => cards.Count <= FlashcardDeck.MaxCardsPerDeck)
            .WithMessage($"Each deck can contain at most {FlashcardDeck.MaxCardsPerDeck} cards.");

        RuleForEach(x => x.Request.Cards).SetValidator(_ =>
        {
            var validator = new InlineValidator<CreateFlashcardCardRequest>();
            FlashcardCardValidationRules.Configure(validator);
            return validator;
        });
    }
}

public sealed class UpsertFlashcardProgressCommandValidator : AbstractValidator<UpsertFlashcardProgressCommand>
{
    public UpsertFlashcardProgressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Status)
            .NotEmpty()
            .Must(status => FlashcardMasteryStatus.Allowed.Contains(status.Trim()))
            .WithMessage("Status must be one of: new, learning, review, mastered.");
    }
}

public sealed class AddFlashcardCardCommandValidator : AbstractValidator<AddFlashcardCardCommand>
{
    public AddFlashcardCardCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request).SetValidator(_ =>
        {
            var validator = new InlineValidator<CreateFlashcardCardRequest>();
            FlashcardCardValidationRules.Configure(validator);
            return validator;
        }).When(x => x.Request is not null);
    }
}

public sealed class RecordFlashcardMatchCommandValidator : AbstractValidator<RecordFlashcardMatchCommand>
{
    public RecordFlashcardMatchCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.ElapsedMs)
            .GreaterThan(0).WithMessage("Completion time must be greater than 0.")
            .LessThanOrEqualTo(3_600_000).WithMessage("Invalid completion time.");
        RuleFor(x => x.Request.MaxCombo)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(500);
    }
}
