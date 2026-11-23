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
            .WithMessage("Consent is required before sending recordings to the AI evaluation service.");
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
            if (evaluation.Status == SpeakingEvaluationStatus.Failed && !settings.IsConfigured)
                throw new SpeakingConflictException("Speaking AI evaluation service is not configured. Your attempt has been saved.");
            if (evaluation.Status == SpeakingEvaluationStatus.Failed && RecordingsExpired(attempt, evaluation, now))
                throw new SpeakingConflictException("Recordings for this attempt have expired from storage and cannot be re-evaluated.");
            if (evaluation.Requeue(now))
                await evaluations.SaveChangesAsync(cancellationToken);
            return await SpeakingAttemptAccess.ToDtoAsync(attempts, attempt, now, cancellationToken, evaluation);
        }

        if (attempt.Status != SpeakingAttemptStatus.Submitted)
            throw new SpeakingConflictException("AI evaluation can only be requested after submitting the Speaking attempt.");
        if (attempt.Questions.Any(question => attempt.CanAcceptRecording(question.Position, now)))
            throw new SpeakingConflictException("The final recording is still being uploaded. Please try again in a few seconds.");
        if (attempt.Questions.All(question => question.RecordingAssetId is null))
            throw new SpeakingConflictException("The attempt has no recordings to evaluate.");
        if (!settings.IsConfigured)
            throw new SpeakingConflictException("Speaking AI evaluation service is not configured. Your attempt has been saved.");
        if (request.Owner.AnonymousSessionHash is { } anonymousSessionHash &&
            await evaluations.HasAnonymousConsentAfterAsync(anonymousSessionHash, attempt.Id, now.AddDays(-1),
                cancellationToken))
            throw new SpeakingConflictException(
                "Anonymous session has already used its AI evaluation in the past 24 hours. Please sign in or try again later.");

        evaluation = SpeakingEvaluation.Create(attempt.Id, settings.SpeechProvider, settings.SpeechLocale,
            settings.LanguageProvider, settings.LanguageModel, settings.RubricVersion, now);
        try
        {
            evaluations.Add(evaluation);
            await evaluations.SaveChangesAsync(cancellationToken);
        }
        catch (SpeakingConflictException)
        {
            // A concurrent request created the job first, so this one reports that job as a repeat call would.
            evaluation = await evaluations.FindForUpdateByAttemptIdAsync(attempt.Id, cancellationToken);
            if (evaluation is null)
                throw;
        }
        return await SpeakingAttemptAccess.ToDtoAsync(attempts, attempt, now, cancellationToken, evaluation);
    }

    /// <summary>A guest's recordings are purged, so questions not transcribed yet could only score zero.</summary>
    private static bool RecordingsExpired(SpeakingAttempt attempt, SpeakingEvaluation evaluation, DateTimeOffset now) =>
        attempt.AnonymousSessionHash is not null &&
        attempt.CreatedAtUtc < now - SpeakingMediaRules.GuestRecordingRetention &&
        attempt.Questions.Any(question => evaluation.FindItem(question.Position) is null);
}
