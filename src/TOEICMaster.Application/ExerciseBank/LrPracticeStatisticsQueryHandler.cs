using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class GetLrPracticeStatisticsQueryHandler(
    ILrPracticeAttemptRepository attemptRepository
) : IRequestHandler<
    GetLrPracticeStatisticsQuery,
    IReadOnlyList<LrPracticeItemStatisticsDto>>
{
    public Task<IReadOnlyList<LrPracticeItemStatisticsDto>> Handle(
        GetLrPracticeStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new InvalidLrPracticeRequestException("Invalid user ID.");

        if (request.Part is < 1 or > 7)
            throw new InvalidLrPracticeRequestException("Part must be between 1 and 7.");

        return attemptRepository.GetStatisticsByUserAndPartAsync(
            request.UserId,
            request.Part,
            cancellationToken);
    }
}
