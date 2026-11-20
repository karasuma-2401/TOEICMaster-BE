
using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record GetLrPracticeStatisticsQuery(
    Guid UserId,
    int Part
) : IRequest<IReadOnlyList<LrPracticeItemStatisticsDto>>;

public sealed record LrPracticeItemStatisticsDto(
    Guid ItemId,
    int AttemptCount,
    int BestScore,
    int TotalQuestions
);
