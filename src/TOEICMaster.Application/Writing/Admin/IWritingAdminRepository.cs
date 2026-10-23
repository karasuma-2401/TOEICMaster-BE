using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Admin;

public interface IWritingAdminRepository
{
    Task<WritingExercisePageDto> GetExercisePageAsync(WritingExerciseFilter filter,
        CancellationToken cancellationToken);
    Task<WritingExerciseDto?> GetExerciseAsync(Guid id, CancellationToken cancellationToken);
    Task<WritingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> MediaAssetExistsAsync(Guid id, CancellationToken cancellationToken);
    Task AddExerciseAsync(WritingExercise exercise, CancellationToken cancellationToken);
    Task<WritingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken);
    Task AddMediaAssetAsync(WritingMediaAsset asset, CancellationToken cancellationToken);
    Task<WritingTestPageDto> GetTestPageAsync(WritingTestFilter filter, CancellationToken cancellationToken);
    Task<WritingTestDto?> GetTestAsync(Guid id, CancellationToken cancellationToken);
    Task<WritingTest?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WritingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
    Task AddTestAsync(WritingTest test, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
