using FluentValidation;
using MediatR;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Evaluations;

public sealed record GrantSpeakingEvaluationConsentInput(bool Accepted);

/// <summary>Whether AI evaluation can be requested, so the UI can explain it before the learner consents.</summary>
public sealed record SpeakingEvaluationAvailabilityDto(bool Available);

public sealed record GetSpeakingEvaluationAvailabilityQuery : IRequest<SpeakingEvaluationAvailabilityDto>;

public sealed class GetSpeakingEvaluationAvailabilityQueryHandler(ISpeakingEvaluationSettings settings)
    : IRequestHandler<GetSpeakingEvaluationAvailabilityQuery, SpeakingEvaluationAvailabilityDto>
{
    public Task<SpeakingEvaluationAvailabilityDto> Handle(GetSpeakingEvaluationAvailabilityQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SpeakingEvaluationAvailabilityDto(settings.IsConfigured));
}

/// <summary>
/// Learner consents to send the recordings to Azure Speech and the transcripts to the language model, which queues
/// the AI evaluation. Calling it again returns the current job, or re-queues a failed one.
/// </summary>
public sealed record GrantSpeakingEvaluationConsentCommand(SpeakingAttemptOwner Owner, Guid AttemptId,
    GrantSpeakingEvaluationConsentInput Input) : IRequest<SpeakingAttemptDto>;

public sealed class GrantSpeakingEvaluationConsentCommandValidator
    : AbstractValidator<GrantSpeakingEvaluationConsentCommand>
{
    public GrantSpeakingEvaluationConsentCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new SpeakingAttemptOwnerValidator());
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.Input.Accepted).Equal(true).OverridePropertyName("Accepted")
            .WithMessage("Cần đồng ý trước khi gửi bản ghi đến dịch vụ chấm AI.");
    }
}

public sealed class GrantSpeakingEvaluationConsentCommandHandler(ISpeakingAttemptRepository attempts,
    ISpeakingEvaluationRepository evaluations, ISpeakingEvaluationSettings settings, TimeProvider timeProvider)
    : IRequestHandler<GrantSpeakingEvaluationConsentCommand, SpeakingAttemptDto>
{
    public async Task<SpeakingAttemptDto> Handle(GrantSpeakingEvaluationConsentCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await SpeakingAttemptAccess.FindOwnedAsync(attempts, request.Owner, request.AttemptId,
            cancellationToken);
        var evaluation = await evaluations.FindForUpdateByAttemptIdAsync(attempt.Id, cancellationToken);
        if (evaluation is not null)
        {
            if (evaluation.Requeue(now))
                await evaluations.SaveChangesAsync(cancellationToken);
            return await SpeakingAttemptAccess.ToDtoAsync(attempts, attempt, now, cancellationToken, evaluation);
        }

        if (attempt.Status != SpeakingAttemptStatus.Submitted)
            throw new SpeakingConflictException("Chỉ có thể chấm AI sau khi đã nộp bài Speaking.");
        if (attempt.Questions.Any(question => attempt.CanAcceptRecording(question.Position, now)))
            throw new SpeakingConflictException("Bản ghi cuối vẫn đang được tải lên. Hãy thử lại sau vài giây.");
        if (attempt.Questions.All(question => question.RecordingAssetId is null))
            throw new SpeakingConflictException("Lượt làm chưa có bản ghi nào để chấm.");
        if (!settings.IsConfigured)
            throw new SpeakingConflictException("Dịch vụ chấm AI Speaking chưa được cấu hình. Bài làm vẫn được lưu.");
        if (request.Owner.AnonymousSessionHash is { } anonymousSessionHash &&
            await evaluations.HasAnonymousConsentAfterAsync(anonymousSessionHash, attempt.Id, now.AddDays(-1),
                cancellationToken))
            throw new SpeakingConflictException(
                "Phiên ẩn danh đã dùng lượt chấm AI trong 24 giờ qua. Hãy đăng nhập hoặc thử lại sau.");

        evaluation = SpeakingEvaluation.Create(attempt.Id, settings.SpeechProvider, settings.SpeechLocale,
            settings.LanguageProvider, settings.LanguageModel, settings.RubricVersion, now);
        evaluations.Add(evaluation);
        await evaluations.SaveChangesAsync(cancellationToken);
        return await SpeakingAttemptAccess.ToDtoAsync(attempts, attempt, now, cancellationToken, evaluation);
    }
}
