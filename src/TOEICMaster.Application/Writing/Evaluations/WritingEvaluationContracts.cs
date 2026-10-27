using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Evaluations;

public sealed record WritingEvaluationAnswer(int Position, int Part, string PromptSnapshotJson,
    string ReviewSnapshotJson, string Answer);

public sealed record WritingEvaluationWork(Guid EvaluationId, string Provider, string Model, string RubricVersion,
    IReadOnlyList<WritingEvaluationAnswer> Answers);

public sealed record WritingEvaluationFeedbackResult(int QuestionPosition, int CriterionScore, string Feedback,
    string SuggestedRevision);

public sealed record WritingEvaluatorResult(string Summary, IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements, IReadOnlyList<WritingEvaluationFeedbackResult> Feedback);

public interface IWritingEvaluator
{
    Task<WritingEvaluatorResult> EvaluateAsync(WritingEvaluationWork work, CancellationToken cancellationToken);
}

public sealed class WritingEvaluatorException(string failureCode, string safeMessage, bool isTransient,
    TimeSpan? retryAfter = null, Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string FailureCode { get; } = failureCode;
    public string SafeMessage { get; } = safeMessage;
    public bool IsTransient { get; } = isTransient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public interface IWritingEvaluationRepository
{
    Task<WritingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, WritingEvaluation>> GetByAttemptIdsAsync(IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken);
    Task<WritingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<WritingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken);
    Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken);
    Task AddAsync(WritingEvaluation evaluation, CancellationToken cancellationToken);
    Task<WritingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
