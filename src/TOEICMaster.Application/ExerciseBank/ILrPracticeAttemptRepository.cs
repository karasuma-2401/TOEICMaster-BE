using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public interface ILrPracticeAttemptRepository
{
    Task AddAsync(
        LrPracticeAttempt attempt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LrPracticeAttempt>> GetByUserAndItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LrPracticeItemStatisticsDto>> GetStatisticsByUserAndPartAsync(
        Guid userId,
        int part,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
