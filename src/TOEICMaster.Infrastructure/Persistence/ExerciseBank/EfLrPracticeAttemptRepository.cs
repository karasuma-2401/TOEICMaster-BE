
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

    public async Task<IReadOnlyList<LrPracticeItemStatisticsDto>> GetStatisticsByUserAndPartAsync(
        Guid userId,
        int part,
        CancellationToken cancellationToken)
    {
        return await BuildStatisticsQuery(userId, part)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<LrPracticeItemStatisticsDto> BuildStatisticsQuery(Guid userId, int part)
    {
        var itemIds = dbContext.LrExerciseBankItems
            .AsNoTracking()
            .Where(item => (int)item.Part == part)
            .Select(item => item.Id);

        return dbContext.LrPracticeAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId && itemIds.Contains(attempt.ItemId))
            .GroupBy(attempt => attempt.ItemId)
            .Select(group => new
            {
                ItemId = group.Key,
                AttemptCount = group.Count(),
                BestScore = group.Max(attempt => attempt.CorrectCount),
                TotalQuestions = group.Max(attempt => attempt.TotalQuestions)
            })
            .OrderBy(statistics => statistics.ItemId)
            .Select(statistics => new LrPracticeItemStatisticsDto(
                statistics.ItemId,
                statistics.AttemptCount,
                statistics.BestScore,
                statistics.TotalQuestions));
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
