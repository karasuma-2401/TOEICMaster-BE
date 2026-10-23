using FluentValidation;
using MediatR;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Attempts;

public sealed record StartWritingAttemptCommand(WritingAttemptOwner Owner, StartWritingAttemptInput Input)
    : IRequest<WritingAttemptDto>;

public sealed record SaveWritingAnswerCommand(WritingAttemptOwner Owner, Guid AttemptId, int Position,
    SaveWritingAnswerInput Input) : IRequest<WritingAttemptDto>;

public sealed record NextWritingAttemptQuestionCommand(WritingAttemptOwner Owner, Guid AttemptId)
    : IRequest<WritingAttemptDto>;

public sealed record SubmitWritingAttemptCommand(WritingAttemptOwner Owner, Guid AttemptId)
    : IRequest<WritingAttemptDto>;

public sealed record GrantWritingEvaluationConsentInput(bool Accepted);

public sealed record GrantWritingEvaluationConsentCommand(WritingAttemptOwner Owner, Guid AttemptId,
    GrantWritingEvaluationConsentInput Input) : IRequest<WritingAttemptDto>;

public sealed class StartWritingAttemptInputValidator : AbstractValidator<StartWritingAttemptInput>
{
    public StartWritingAttemptInputValidator()
    {
        RuleFor(input => input.TestId).NotEmpty();
        RuleFor(input => input.Mode).Must(mode => mode is not null && IsMode(mode));
    }

    private static bool IsMode(string value) => value.Trim().ToUpperInvariant() is "FULL" or "PART_1" or "PART_2" or "PART_3";
}

public sealed class SaveWritingAnswerInputValidator : AbstractValidator<SaveWritingAnswerInput>
{
    public SaveWritingAnswerInputValidator() => RuleFor(input => input.Answer).NotNull().MaximumLength(12_000);
}

public sealed class StartWritingAttemptCommandValidator : AbstractValidator<StartWritingAttemptCommand>
{
    public StartWritingAttemptCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(command => command.Input).SetValidator(new StartWritingAttemptInputValidator());
    }
}

public sealed class SaveWritingAnswerCommandValidator : AbstractValidator<SaveWritingAnswerCommand>
{
    public SaveWritingAnswerCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.Position).GreaterThan(0);
        RuleFor(command => command.Input).SetValidator(new SaveWritingAnswerInputValidator());
    }
}

public sealed class NextWritingAttemptQuestionCommandValidator : AbstractValidator<NextWritingAttemptQuestionCommand>
{
    public NextWritingAttemptQuestionCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(command => command.AttemptId).NotEmpty();
    }
}

public sealed class SubmitWritingAttemptCommandValidator : AbstractValidator<SubmitWritingAttemptCommand>
{
    public SubmitWritingAttemptCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(command => command.AttemptId).NotEmpty();
    }
}

public sealed class GrantWritingEvaluationConsentCommandValidator : AbstractValidator<GrantWritingEvaluationConsentCommand>
{
    public GrantWritingEvaluationConsentCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.Input.Accepted).Equal(true)
            .WithMessage("Cần đồng ý trước khi gửi bài làm đến dịch vụ chấm AI.");
    }
}

public sealed class StartWritingAttemptCommandHandler(IWritingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<StartWritingAttemptCommand, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(StartWritingAttemptCommand request, CancellationToken cancellationToken)
    {
        var test = await repository.FindPublishedTestForAttemptAsync(request.Input.TestId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        var mode = WritingAttemptMapping.ParseMode(request.Input.Mode);
        var part = mode.PartForMode();
        var selected = test.Questions.Where(question => part is null || question.Part == part)
            .OrderBy(question => question.Part).ThenBy(question => question.SequenceNumber).ToArray();
        if (selected.Length == 0 || selected.Any(question => string.IsNullOrWhiteSpace(question.SnapshotJson)))
            throw new WritingAttemptConflictException("Đề Writing chưa sẵn sàng để làm bài.");

        var attemptId = Guid.NewGuid();
        var questions = selected.Select((question, index) =>
        {
            var timing = question.Part.TimingFor();
            return WritingAttemptQuestion.Create(attemptId, question.Id, index + 1, question.Part,
                timing.PreparationSeconds, timing.ResponseSeconds,
                WritingAttemptMapping.ToPromptSnapshotJson(question.SnapshotJson!),
                WritingAttemptMapping.ToReviewSnapshotJson(question.SnapshotJson!));
        }).ToArray();
        var now = timeProvider.GetUtcNow();
        var attempt = WritingAttempt.Create(attemptId, test.Id, test.Code, test.Title, mode, request.Owner.UserId,
            request.Owner.AnonymousSessionHash, questions, now);
        await repository.AddAttemptAsync(attempt, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now);
    }
}

public sealed class SaveWritingAnswerCommandHandler(IWritingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SaveWritingAnswerCommand, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(SaveWritingAnswerCommand request, CancellationToken cancellationToken)
    {
        var attempt = await WritingAttemptCommandHelpers.FindOwnedAttemptAsync(repository, request.Owner,
            request.AttemptId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        try
        {
            attempt.SaveAnswer(request.Position, request.Input.Answer, now);
        }
        catch (InvalidOperationException exception)
        {
            throw new WritingAttemptConflictException(exception.Message);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now);
    }
}

public sealed class NextWritingAttemptQuestionCommandHandler(IWritingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<NextWritingAttemptQuestionCommand, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(NextWritingAttemptQuestionCommand request,
        CancellationToken cancellationToken)
    {
        var attempt = await WritingAttemptCommandHelpers.FindOwnedAttemptAsync(repository, request.Owner,
            request.AttemptId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        try
        {
            attempt.Next(now);
        }
        catch (InvalidOperationException exception)
        {
            throw new WritingAttemptConflictException(exception.Message);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now);
    }
}

public sealed class SubmitWritingAttemptCommandHandler(IWritingAttemptRepository repository,
    IWritingEvaluationRepository evaluationRepository, IWritingEvaluationSettings evaluationSettings,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitWritingAttemptCommand, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(SubmitWritingAttemptCommand request,
        CancellationToken cancellationToken)
    {
        var attempt = await WritingAttemptCommandHelpers.FindOwnedAttemptAsync(repository, request.Owner,
            request.AttemptId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        attempt.Submit(now);
        var evaluation = await evaluationRepository.FindForUpdateByAttemptIdAsync(attempt.Id, cancellationToken);
        if (evaluation is null)
        {
            evaluation = WritingEvaluation.Create(attempt.Id, evaluationSettings.Provider, evaluationSettings.Model,
                evaluationSettings.RubricVersion, now);
            await evaluationRepository.AddAsync(evaluation, cancellationToken);
        }
        await evaluationRepository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now, evaluation);
    }
}

public sealed class GrantWritingEvaluationConsentCommandHandler(IWritingAttemptRepository repository,
    IWritingEvaluationRepository evaluationRepository, IWritingEvaluationSettings evaluationSettings,
    TimeProvider timeProvider) : IRequestHandler<GrantWritingEvaluationConsentCommand, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(GrantWritingEvaluationConsentCommand request,
        CancellationToken cancellationToken)
    {
        var attempt = await WritingAttemptCommandHelpers.FindOwnedAttemptAsync(repository, request.Owner,
            request.AttemptId, cancellationToken);
        if (attempt.Status != WritingAttemptStatus.SubmittedAwaitingEvaluation)
            throw new WritingAttemptConflictException("Chỉ có thể chấm AI sau khi đã nộp bài Writing.");

        var now = timeProvider.GetUtcNow();
        var evaluation = await evaluationRepository.FindForUpdateByAttemptIdAsync(attempt.Id, cancellationToken);
        if (request.Owner.AnonymousSessionHash is { } anonymousSessionHash &&
            (evaluation is null || evaluation.ConsentAcceptedAtUtc is null) &&
            await evaluationRepository.HasAnonymousConsentAfterAsync(anonymousSessionHash, attempt.Id, now.AddDays(-1),
                cancellationToken))
            throw new WritingAttemptConflictException(
                "Phiên ẩn danh đã dùng lượt chấm AI trong 24 giờ qua. Hãy đăng nhập hoặc thử lại sau.");
        if (evaluation is null)
        {
            evaluation = WritingEvaluation.Create(attempt.Id, evaluationSettings.Provider, evaluationSettings.Model,
                evaluationSettings.RubricVersion, now);
            await evaluationRepository.AddAsync(evaluation, cancellationToken);
        }
        evaluation.GrantConsentAndQueue(now);
        await evaluationRepository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now, evaluation);
    }
}

internal static class WritingAttemptCommandHelpers
{
    public static async Task<WritingAttempt> FindOwnedAttemptAsync(IWritingAttemptRepository repository,
        WritingAttemptOwner owner, Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(attemptId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        GetWritingAttemptQueryHandler.EnsureOwner(attempt, owner);
        return attempt;
    }
}
