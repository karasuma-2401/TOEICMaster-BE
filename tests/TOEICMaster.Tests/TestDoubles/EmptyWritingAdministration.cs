using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class EmptyWritingAdminRepository : IWritingAdminRepository
{
    public Task<WritingExercisePageDto> GetExercisePageAsync(WritingExerciseFilter filter,
        CancellationToken cancellationToken) =>
        Task.FromResult(new WritingExercisePageDto([], filter.PageNumber, filter.PageSize, 0));

    public Task<WritingExerciseDto?> GetExerciseAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WritingExerciseDto?>(null);

    public Task<WritingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WritingExercise?>(null);

    public Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> MediaAssetExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task AddExerciseAsync(WritingExercise exercise, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<WritingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WritingMediaAsset?>(null);

    public Task AddMediaAssetAsync(WritingMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<WritingTestPageDto> GetTestPageAsync(WritingTestFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(new WritingTestPageDto([], filter.PageNumber, filter.PageSize, 0));

    public Task<WritingTestDto?> GetTestAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WritingTestDto?>(null);

    public Task<WritingTest?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WritingTest?>(null);

    public Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<WritingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WritingExercise>>([]);

    public Task AddTestAsync(WritingTest test, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class EmptyWritingImageStore : IWritingImageStore
{
    public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<WritingImageContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(new WritingImageContent([], "image/png"));

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class EmptyWritingAttemptRepository : IWritingAttemptRepository
{
    public Task<WritingPublicTestPageDto> GetPublishedTestsAsync(string? query, int pageNumber, int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromResult(new WritingPublicTestPageDto([], pageNumber, pageSize, 0));

    public Task<WritingPublicTestDto?> GetPublishedTestAsync(Guid testId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingPublicTestDto?>(null);

    public Task<WritingTest?> FindPublishedTestForAttemptAsync(Guid testId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingTest?>(null);

    public Task<WritingMediaAsset?> FindPublishedMediaAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingMediaAsset?>(null);

    public Task<WritingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingAttempt?>(null);

    public Task<WritingAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingAttempt?>(null);

    public Task<IReadOnlyList<WritingAttempt>> GetAttemptsAsync(WritingAttemptOwner owner,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WritingAttempt>>([]);

    public Task AddAttemptAsync(WritingAttempt attempt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class EmptyWritingEvaluationRepository : IWritingEvaluationRepository
{
    public Task<WritingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingEvaluation?>(null);

    public Task<IReadOnlyDictionary<Guid, WritingEvaluation>> GetByAttemptIdsAsync(IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, WritingEvaluation>>(new Dictionary<Guid, WritingEvaluation>());

    public Task<WritingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingEvaluation?>(null);

    public Task<WritingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        Task.FromResult<WritingEvaluation?>(null);

    public Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task AddAsync(WritingEvaluation evaluation, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<WritingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken) => Task.FromResult<WritingEvaluationWork?>(null);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class StaticWritingEvaluationSettings : IWritingEvaluationSettings
{
    public string Provider => "gemini";
    public string Model => "gemini-2.5-flash-lite";
    public string RubricVersion => "toeic-writing-v1";
}

public sealed class EmptyWritingExerciseImportParser : IWritingExerciseImportParser
{
    public WritingExerciseImportPreviewDto Preview(int part, string fileName, byte[] content) =>
        new(part, fileName, []);
}
