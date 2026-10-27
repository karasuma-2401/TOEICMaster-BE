using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Attempts;

public interface IWritingAttemptRepository
{
    Task<WritingPublicTestPageDto> GetPublishedTestsAsync(string? query, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<WritingPublicTestDto?> GetPublishedTestAsync(Guid testId, CancellationToken cancellationToken);
    Task<WritingTest?> FindPublishedTestForAttemptAsync(Guid testId, CancellationToken cancellationToken);
    Task<WritingMediaAsset?> FindPublishedMediaAssetAsync(Guid assetId, CancellationToken cancellationToken);
    Task<WritingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<WritingAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WritingAttempt>> GetAttemptsAsync(WritingAttemptOwner owner, CancellationToken cancellationToken);
    Task AddAttemptAsync(WritingAttempt attempt, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
