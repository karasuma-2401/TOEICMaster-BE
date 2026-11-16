
using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class GetLrPracticeStatisticsQueryHandler(
    ILrPracticeAttemptRepository attemptRepository
) : IRequestHandler<
    GetLrPracticeStatisticsQuery,
    IReadOnlyList<LrPracticeItemStatisticsDto>>
{
    public async Task<IReadOnlyList<LrPracticeItemStatisticsDto>> Handle(
        GetLrPracticeStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("Invalid user ID.");

        if (request.Part is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(request.Part));

        var attempts = await attemptRepository.GetByUserAndPartAsync(
            request.UserId,
            request.Part,
            cancellationToken);

        var statistics = attempts
            .GroupBy(attempt => attempt.ItemId)
            .Select(group => new LrPracticeItemStatisticsDto(
                group.Key,
                group.Count(),
                group.Max(attempt => attempt.CorrectCount),
                group.First().TotalQuestions
            ))
            .ToList();

        return statistics;
    }
}
