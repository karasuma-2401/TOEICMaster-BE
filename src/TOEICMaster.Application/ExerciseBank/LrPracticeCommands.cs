using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record SubmitLrPracticeCommand(
    Guid UserId,
    Guid ItemId,
    LrPracticeSubmitRequest Request
) : IRequest<LrPracticeSubmitResultDto>;
