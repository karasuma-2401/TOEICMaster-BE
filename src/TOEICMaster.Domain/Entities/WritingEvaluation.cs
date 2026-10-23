using System.Text.Json;

namespace TOEICMaster.Domain.Entities;

public enum WritingEvaluationStatus
{
    ConsentRequired = 1,
    Queued = 2,
    Processing = 3,
    Retrying = 4,
    Completed = 5,
    Failed = 6
}

/// <summary>
/// A durable AI-evaluation job.  The submitted attempt stays immutable while this job moves through its lifecycle.
/// </summary>
public sealed class WritingEvaluation
{
    private readonly List<WritingEvaluationFeedback> _feedback = [];

    private WritingEvaluation()
    {
    }

    private WritingEvaluation(Guid id, Guid writingAttemptId, string provider, string model, string rubricVersion,
        DateTimeOffset now)
    {
        Id = id;
        WritingAttemptId = writingAttemptId;
        Provider = provider;
        Model = model;
        RubricVersion = rubricVersion;
        Status = WritingEvaluationStatus.ConsentRequired;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid WritingAttemptId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string RubricVersion { get; private set; } = string.Empty;
    public WritingEvaluationStatus Status { get; private set; }
    public DateTimeOffset? ConsentAcceptedAtUtc { get; private set; }
    public DateTimeOffset? QueuedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public int? EstimatedScore { get; private set; }
    public string? Summary { get; private set; }
    public string? StrengthsJson { get; private set; }
    public string? ImprovementsJson { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<WritingEvaluationFeedback> Feedback => _feedback;

    public static WritingEvaluation Create(Guid writingAttemptId, string provider, string model, string rubricVersion,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubricVersion);
        return new WritingEvaluation(Guid.NewGuid(), writingAttemptId, provider, model, rubricVersion, now);
    }

    /// <summary>Records explicit consent and queues the first evaluation. Failed jobs reuse this consent on a user retry.</summary>
    public bool GrantConsentAndQueue(DateTimeOffset now)
    {
        if (Status is not (WritingEvaluationStatus.ConsentRequired or WritingEvaluationStatus.Failed))
            return false;

        ConsentAcceptedAtUtc ??= now;
        Status = WritingEvaluationStatus.Queued;
        QueuedAtUtc = now;
        StartedAtUtc = null;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = now;
        CompletedAtUtc = null;
        FailureCode = null;
        FailureMessage = null;
        Touch(now);
        return true;
    }

    public bool CanBeClaimed(DateTimeOffset now) =>
        (Status is WritingEvaluationStatus.Queued or WritingEvaluationStatus.Retrying) &&
        NextAttemptAtUtc is not null && NextAttemptAtUtc <= now ||
        Status == WritingEvaluationStatus.Processing && LeaseExpiresAtUtc is not null && LeaseExpiresAtUtc <= now;

    public void BeginProcessing(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc)
    {
        if (!CanBeClaimed(now))
            throw new InvalidOperationException("This Writing evaluation is not ready to be processed.");
        if (ConsentAcceptedAtUtc is null)
            throw new InvalidOperationException("Writing evaluation consent is required.");

        Status = WritingEvaluationStatus.Processing;
        StartedAtUtc ??= now;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        NextAttemptAtUtc = null;
        Touch(now);
    }

    public void Complete(int estimatedScore, string summary, IReadOnlyList<string> strengths,
        IReadOnlyList<string> improvements, IReadOnlyList<WritingEvaluationFeedback> feedback, DateTimeOffset now)
    {
        if (Status != WritingEvaluationStatus.Processing)
            throw new InvalidOperationException("Only an active Writing evaluation can be completed.");
        if (estimatedScore is < 0 or > 200 || estimatedScore % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedScore), "Writing scores must be between 0 and 200 in steps of 10.");
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        EstimatedScore = estimatedScore;
        Summary = summary;
        StrengthsJson = JsonSerializer.Serialize(strengths);
        ImprovementsJson = JsonSerializer.Serialize(improvements);
        _feedback.Clear();
        _feedback.AddRange(feedback);
        Status = WritingEvaluationStatus.Completed;
        CompletedAtUtc = now;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FailureCode = null;
        FailureMessage = null;
        Touch(now);
    }

    public void ScheduleRetry(string failureCode, string failureMessage, DateTimeOffset nextAttemptAtUtc,
        DateTimeOffset now)
    {
        EnsureProcessing();
        RetryCount++;
        Status = WritingEvaluationStatus.Retrying;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        Touch(now);
    }

    public void Fail(string failureCode, string failureMessage, DateTimeOffset now)
    {
        EnsureProcessing();
        RetryCount++;
        Status = WritingEvaluationStatus.Failed;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        Touch(now);
    }

    private void EnsureProcessing()
    {
        if (Status != WritingEvaluationStatus.Processing)
            throw new InvalidOperationException("Only an active Writing evaluation can be updated.");
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        Version++;
    }
}

public sealed class WritingEvaluationFeedback
{
    private WritingEvaluationFeedback()
    {
    }

    private WritingEvaluationFeedback(Guid id, Guid writingEvaluationId, int questionPosition, int criterionScore,
        string feedback, string suggestedRevision)
    {
        Id = id;
        WritingEvaluationId = writingEvaluationId;
        QuestionPosition = questionPosition;
        CriterionScore = criterionScore;
        Feedback = feedback;
        SuggestedRevision = suggestedRevision;
    }

    public Guid Id { get; private set; }
    public Guid WritingEvaluationId { get; private set; }
    public int QuestionPosition { get; private set; }
    public int CriterionScore { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public string SuggestedRevision { get; private set; } = string.Empty;

    public static WritingEvaluationFeedback Create(Guid writingEvaluationId, int questionPosition, int criterionScore,
        string feedback, string suggestedRevision) => new(Guid.NewGuid(), writingEvaluationId, questionPosition,
        criterionScore, feedback, suggestedRevision);
}
