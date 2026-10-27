using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Writing;

public sealed class EfWritingAdminRepository(ToeicDbContext context) : IWritingAdminRepository
{
    public async Task<WritingExercisePageDto> GetExercisePageAsync(WritingExerciseFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.WritingExercises.AsNoTracking();
        if (filter.Part is not null)
            query = query.Where(exercise => exercise.Part == WritingAdminMapping.ParsePart(filter.Part.Value));
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim().ToUpperInvariant();
            query = query.Where(exercise => exercise.Code.ToUpper().Contains(term) ||
                                            exercise.Topic.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Difficulty))
            query = query.Where(exercise => exercise.Difficulty ==
                WritingAdminMapping.ParseDifficulty(filter.Difficulty.Trim()));
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(exercise => exercise.Status ==
                WritingAdminMapping.ParseExerciseStatus(filter.Status.Trim()));
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);
        var items = await query.OrderByDescending(exercise => exercise.UpdatedAtUtc).ThenBy(exercise => exercise.Id)
            .Skip(offset).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new WritingExercisePageDto(items.Select(WritingAdminMapping.ToDto).ToArray(), filter.PageNumber,
            filter.PageSize, totalCount);
    }

    public async Task<WritingExerciseDto?> GetExerciseAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await context.WritingExercises.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id,
            cancellationToken);
        return exercise?.ToDto();
    }

    public Task<WritingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.WritingExercises.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = code.ToUpperInvariant();
        return context.WritingExercises.AsNoTracking().AnyAsync(exercise => exercise.Code.ToUpper() == normalized &&
            (!excludingId.HasValue || exercise.Id != excludingId.Value), cancellationToken);
    }

    public Task<bool> MediaAssetExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.WritingMediaAssets.AsNoTracking().AnyAsync(asset => asset.Id == id, cancellationToken);

    public Task AddExerciseAsync(WritingExercise exercise, CancellationToken cancellationToken)
    {
        context.WritingExercises.Add(exercise);
        return Task.CompletedTask;
    }

    public Task<WritingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        context.WritingMediaAssets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public Task AddMediaAssetAsync(WritingMediaAsset asset, CancellationToken cancellationToken)
    {
        context.WritingMediaAssets.Add(asset);
        return Task.CompletedTask;
    }

    public async Task<WritingTestPageDto> GetTestPageAsync(WritingTestFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<WritingTest> query = context.WritingTests.AsNoTracking().Include(test => test.Questions);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim().ToUpperInvariant();
            query = query.Where(test => test.Code.ToUpper().Contains(term) || test.Title.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(test => test.Status == WritingAdminMapping.ParseTestStatus(filter.Status.Trim()));
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);
        var items = await query.OrderByDescending(test => test.UpdatedAtUtc).ThenBy(test => test.Id).Skip(offset)
            .Take(filter.PageSize).ToListAsync(cancellationToken);
        return new WritingTestPageDto(items.Select(WritingAdminMapping.ToDto).ToArray(), filter.PageNumber,
            filter.PageSize, totalCount);
    }

    public async Task<WritingTestDto?> GetTestAsync(Guid id, CancellationToken cancellationToken)
    {
        var test = await context.WritingTests.AsNoTracking().Include(item => item.Questions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return test?.ToDto();
    }

    public Task<WritingTest?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.WritingTests.Include(test => test.Questions).SingleOrDefaultAsync(test => test.Id == id,
            cancellationToken);

    public Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = code.ToUpperInvariant();
        return context.WritingTests.AsNoTracking().AnyAsync(test => test.Code.ToUpper() == normalized &&
            (!excludingId.HasValue || test.Id != excludingId.Value), cancellationToken);
    }

    public async Task<IReadOnlyList<WritingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        await context.WritingExercises.AsNoTracking().Where(exercise => ids.Contains(exercise.Id))
            .ToListAsync(cancellationToken);

    public Task AddTestAsync(WritingTest test, CancellationToken cancellationToken)
    {
        context.WritingTests.Add(test);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WritingConflictException("Dữ liệu đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        }
    }
}
