using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

public interface ISpeakingAdminRepository
{
    Task<SpeakingAdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<SpeakingPageDto<SpeakingExercise>> GetExercisePageAsync(SpeakingExerciseFilter filter,
        CancellationToken cancellationToken);
    Task<SpeakingExercise?> FindExerciseAsync(Guid id, CancellationToken cancellationToken);
    Task<SpeakingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SpeakingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
    Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    void AddExercise(SpeakingExercise exercise);

    Task<SpeakingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken);
    void AddMediaAsset(SpeakingMediaAsset asset);

    Task<SpeakingPageDto<SpeakingTestWithSlots>> GetTestPageAsync(SpeakingTestFilter filter,
        CancellationToken cancellationToken);
    Task<SpeakingTestWithSlots?> FindTestAsync(Guid id, CancellationToken cancellationToken);
    Task<SpeakingTestWithSlots?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    void AddTest(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots);
    /// <summary>Atomically replaces the slots of a tracked draft test and saves the test.</summary>
    Task ReplaceDraftSlotsAndSaveAsync(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots,
        CancellationToken cancellationToken);

    /// <summary>Saves changes; a stale version or a duplicate code raises <see cref="SpeakingConflictException"/>.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
