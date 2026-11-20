
using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record GetLrPracticeItemsQuery(
    int Part,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<LrPracticeItemPageDto>;

public sealed record GetLrPracticeItemDetailQuery(
    Guid ItemId
) : IRequest<LrPracticeDetailDto?>;

public sealed record LrPracticeItemPageDto(
    IReadOnlyList<LrPracticeItemDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages
);
