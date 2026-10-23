using TOEICMaster.Application.Writing.Admin;
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

public sealed class EmptyWritingExerciseImportParser : IWritingExerciseImportParser
{
    public WritingExerciseImportPreviewDto Preview(int part, string fileName, byte[] content) =>
        new(part, fileName, []);
}
