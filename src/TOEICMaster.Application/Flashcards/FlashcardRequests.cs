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

public sealed record AddFlashcardCardCommand(
    Guid UserId,
    Guid DeckId,
    CreateFlashcardCardRequest Request) : IRequest<FlashcardCardDto?>;

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
            .Must(cards => cards.Count <= 200).WithMessage("Each deck can contain at most 200 cards.");

        RuleForEach(x => x.Request.Cards).ChildRules(card =>
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
        RuleFor(x => x.Request.Term)
            .NotEmpty().WithMessage("English term cannot be empty.")
            .MaximumLength(120);
        RuleFor(x => x.Request.MeaningVi)
            .NotEmpty().WithMessage("Vietnamese meaning cannot be empty.")
            .MaximumLength(300);
        RuleFor(x => x.Request.Phonetic)
            .MaximumLength(120)
            .When(x => x.Request.Phonetic is not null);
        RuleFor(x => x.Request.PartOfSpeech)
            .MaximumLength(40)
            .When(x => x.Request.PartOfSpeech is not null);
        RuleFor(x => x.Request.ExampleEn)
            .MaximumLength(400)
            .When(x => x.Request.ExampleEn is not null);
        RuleFor(x => x.Request.ExampleVi)
            .MaximumLength(400)
            .When(x => x.Request.ExampleVi is not null);
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
