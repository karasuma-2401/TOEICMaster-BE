using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams;

public interface IExamRepository
{
    Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken);
    Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScoreProfileDto>> GetScoreProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScoreProfileDto>>([]);
    Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken);
    Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken);
    Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken);
    Task AddExamMediaUploadIntentAsync(ExamMediaUploadIntent intent, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<ExamMediaUploadIntent?> FindExamMediaUploadIntentAsync(Guid intentId, CancellationToken cancellationToken) =>
        Task.FromResult<ExamMediaUploadIntent?>(null);
    Task<IReadOnlyList<ExamMediaUploadIntent>> GetExpiredPendingExamMediaUploadIntentsAsync(DateTimeOffset now,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExamMediaUploadIntent>>([]);
    Task AddExamAsync(Exam exam, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
