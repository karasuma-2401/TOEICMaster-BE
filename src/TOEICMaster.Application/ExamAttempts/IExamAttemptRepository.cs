using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

public interface IExamAttemptRepository
{
    Task<ExamRevision?> FindPublishedRevisionForExamAsync(Guid examId, CancellationToken cancellationToken);
    Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken);
    Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, (int Number, int Part)>> GetQuestionMetadataAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);
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
