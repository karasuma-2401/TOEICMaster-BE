
using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Persistence.ExerciseBank;

public sealed class EfLrPracticeAttemptRepository(
    ToeicDbContext dbContext) : ILrPracticeAttemptRepository
{
    public async Task AddAsync(
        LrPracticeAttempt attempt,
        CancellationToken cancellationToken)
    {
        await dbContext.LrPracticeAttempts.AddAsync(
            attempt,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LrPracticeAttempt>> GetByUserAndItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        return await dbContext.LrPracticeAttempts
            .AsNoTracking()
            .Include(x => x.Answers)
            .Where(x => x.UserId == userId && x.ItemId == itemId)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LrPracticeAttempt>> GetByUserAndPartAsync(
        Guid userId,
        int part,
        CancellationToken cancellationToken)
    {
        var itemIds = await dbContext.LrExerciseBankItems
            .AsNoTracking()
            .Where(x => (int)x.Part == part)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return await dbContext.LrPracticeAttempts
            .AsNoTracking()
            .Where(x => x.UserId == userId && itemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
