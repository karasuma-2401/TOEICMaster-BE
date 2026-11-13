using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

public sealed record ExamHeader(Guid Id, string Code, string Title);

/// <summary>Facts about a question that are needed to validate and store an answer.</summary>
public sealed record ExamQuestionMetadata(Guid RevisionId, int Number, int Part, bool HasOptionD);

public interface IExamAttemptRepository
{
    Task<ExamHeader?> GetExamHeaderAsync(Guid examId, CancellationToken cancellationToken);
    Task<ExamRevision?> FindPublishedRevisionForExamAsync(Guid examId, CancellationToken cancellationToken);
    Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken);
    Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, ExamQuestionMetadata>> GetQuestionMetadataAsync(
        IReadOnlyCollection<Guid> questionIds,
        CancellationToken cancellationToken);
    Task<ExamAttempt?> FindAttemptForUpdateAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken);
    Task<ExamAttempt?> GetAttemptAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken);
    Task<Guid?> FindActiveAttemptIdAsync(Guid userId, Guid examId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ExamAttemptHistoryItemDto> Items, int TotalCount)> GetAttemptHistoryAsync(
        Guid userId,
        int pageIndex,
        int pageSize,
        Guid? examId,
        CancellationToken cancellationToken);
    void AddAttempt(ExamAttempt attempt);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
