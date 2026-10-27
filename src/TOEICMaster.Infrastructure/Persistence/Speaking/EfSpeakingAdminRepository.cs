using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Speaking;

public sealed class EfSpeakingAdminRepository(ToeicDbContext context) : ISpeakingAdminRepository
{
    public async Task<SpeakingAdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var exercises = await context.SpeakingExercises.AsNoTracking().GroupBy(exercise => exercise.Status)
            .Select(group => new { group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var tests = await context.SpeakingTests.AsNoTracking().GroupBy(test => test.Status)
            .Select(group => new { group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        int ExerciseCount(SpeakingExerciseStatus status) => exercises.Where(item => item.Key == status)
            .Sum(item => item.Count);
        int TestCount(SpeakingTestStatus status) => tests.Where(item => item.Key == status).Sum(item => item.Count);
        return new SpeakingAdminSummaryDto(
            new SpeakingExerciseCountsDto(exercises.Sum(item => item.Count),
                ExerciseCount(SpeakingExerciseStatus.Published), ExerciseCount(SpeakingExerciseStatus.Draft),
                ExerciseCount(SpeakingExerciseStatus.Archived)),
            new SpeakingTestCountsDto(tests.Sum(item => item.Count), TestCount(SpeakingTestStatus.Published),
                TestCount(SpeakingTestStatus.Draft)));
    }

    public async Task<SpeakingPageDto<SpeakingExercise>> GetExercisePageAsync(SpeakingExerciseFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.SpeakingExercises.AsNoTracking();
        if (filter.Part is { } part)
            query = query.Where(exercise => exercise.Part == part);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{EscapeLike(filter.Query.Trim())}%";
            query = query.Where(exercise => EF.Functions.ILike(exercise.Code, pattern, "\\") ||
                                            EF.Functions.ILike(exercise.Topic, pattern, "\\"));
        }
        if (SpeakingCodes.TryParseDifficulty(filter.Difficulty, out var difficulty))
            query = query.Where(exercise => exercise.Difficulty == difficulty);
        if (SpeakingCodes.TryParseExerciseStatus(filter.Status, out var status))
            query = query.Where(exercise => exercise.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(exercise => exercise.UpdatedAtUtc).ThenBy(exercise => exercise.Id)
            .Skip(checked((filter.PageNumber - 1) * filter.PageSize)).Take(filter.PageSize)
            .ToListAsync(cancellationToken);
        return new SpeakingPageDto<SpeakingExercise>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<SpeakingExercise?> FindExerciseAsync(Guid id, CancellationToken cancellationToken) =>
        context.SpeakingExercises.AsNoTracking().SingleOrDefaultAsync(exercise => exercise.Id == id,
            cancellationToken);

    public Task<SpeakingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.SpeakingExercises.SingleOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SpeakingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.SpeakingExercises.AsNoTracking().Where(exercise => ids.Contains(exercise.Id))
                .ToListAsync(cancellationToken);

    public Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = code.ToUpperInvariant();
        return context.SpeakingExercises.AsNoTracking().AnyAsync(exercise => exercise.Code.ToUpper() == normalized &&
            (excludingId == null || exercise.Id != excludingId), cancellationToken);
    }

    public void AddExercise(SpeakingExercise exercise) => context.SpeakingExercises.Add(exercise);

    public Task<SpeakingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        context.SpeakingMediaAssets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public void AddMediaAsset(SpeakingMediaAsset asset) => context.SpeakingMediaAssets.Add(asset);

    public async Task<SpeakingPageDto<SpeakingTestWithSlots>> GetTestPageAsync(SpeakingTestFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.SpeakingTests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{EscapeLike(filter.Query.Trim())}%";
            query = query.Where(test => EF.Functions.ILike(test.Code, pattern, "\\") ||
                                        EF.Functions.ILike(test.Title, pattern, "\\"));
        }
        if (SpeakingCodes.TryParseTestStatus(filter.Status, out var status))
            query = query.Where(test => test.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var tests = await query.OrderByDescending(test => test.UpdatedAtUtc).ThenBy(test => test.Id)
            .Skip(checked((filter.PageNumber - 1) * filter.PageSize)).Take(filter.PageSize)
            .ToListAsync(cancellationToken);
        var slots = await SlotsAsync(tests.Select(test => test.Id).ToArray(), tracked: false, cancellationToken);
        return new SpeakingPageDto<SpeakingTestWithSlots>(tests.Select(test =>
                new SpeakingTestWithSlots(test, slots.GetValueOrDefault(test.Id) ?? [])).ToArray(),
            filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<SpeakingTestWithSlots?> FindTestAsync(Guid id, CancellationToken cancellationToken) =>
        FindTestAsync(id, tracked: false, cancellationToken);

    public Task<SpeakingTestWithSlots?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindTestAsync(id, tracked: true, cancellationToken);

    public Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = code.ToUpperInvariant();
        return context.SpeakingTests.AsNoTracking().AnyAsync(test => test.Code.ToUpper() == normalized &&
            (excludingId == null || test.Id != excludingId), cancellationToken);
    }

    public void AddTest(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots)
    {
        context.SpeakingTests.Add(test);
        context.SpeakingTestExercises.AddRange(slots);
    }

    public async Task ReplaceDraftSlotsAndSaveAsync(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots,
        CancellationToken cancellationToken)
    {
        // Old slots are deleted with SQL first, so swapping two exercises never hits the (test, exercise) unique index.
        foreach (var tracked in context.ChangeTracker.Entries<SpeakingTestExercise>()
                     .Where(entry => entry.Entity.TestId == test.Id).ToArray())
            tracked.State = EntityState.Detached;
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SpeakingTestExercises.Where(slot => slot.TestId == test.Id)
                .ExecuteDeleteAsync(cancellationToken);
            context.SpeakingTestExercises.AddRange(slots);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SpeakingConflictException("Dữ liệu đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            throw new SpeakingConflictException("Mã đã tồn tại hoặc bài tập bị trùng trong đề.");
        }
    }

    private async Task<SpeakingTestWithSlots?> FindTestAsync(Guid id, bool tracked,
        CancellationToken cancellationToken)
    {
        var tests = tracked ? context.SpeakingTests : context.SpeakingTests.AsNoTracking();
        var test = await tests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (test is null)
            return null;
        var slots = await SlotsAsync([id], tracked, cancellationToken);
        return new SpeakingTestWithSlots(test, slots.GetValueOrDefault(id) ?? []);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<SpeakingTestExercise>>> SlotsAsync(Guid[] testIds,
        bool tracked, CancellationToken cancellationToken)
    {
        var slots = tracked ? context.SpeakingTestExercises : context.SpeakingTestExercises.AsNoTracking();
        var items = await slots.Where(slot => testIds.Contains(slot.TestId)).OrderBy(slot => slot.Part)
            .ThenBy(slot => slot.SequenceNumber).ToListAsync(cancellationToken);
        return items.GroupBy(slot => slot.TestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SpeakingTestExercise>)group.ToArray());
    }

    internal static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
