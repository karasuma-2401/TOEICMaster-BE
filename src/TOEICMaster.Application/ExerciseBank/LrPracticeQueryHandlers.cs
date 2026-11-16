
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class GetLrPracticeItemsQueryHandler(
    ILrExerciseBankRepository repository
) : IRequestHandler<GetLrPracticeItemsQuery, LrPracticeItemPageDto>
{
    public async Task<LrPracticeItemPageDto> Handle(
        GetLrPracticeItemsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Part is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(request.Part));

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var (items, totalCount) = await repository.GetPublishedPageAsync(
            request.Part,
            pageNumber,
            pageSize,
            cancellationToken);

        var result = items.Select(item => new LrPracticeItemDto(
            item.Id,
            (int)item.Part,
            item.Kind.ToString(),
            item.Code,
            item.Topic,
            item.Difficulty.ToString(),
            item.Context,
            item.AudioUrl,
            item.ImageUrl,
            item.Questions.Count
        )).ToList();

        return new LrPracticeItemPageDto(
            result,
            pageNumber,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)
        );
    }
}

public sealed class GetLrPracticeItemDetailQueryHandler(
    ILrExerciseBankRepository repository
) : IRequestHandler<GetLrPracticeItemDetailQuery, LrPracticeDetailDto?>
{
    public async Task<LrPracticeDetailDto?> Handle(
        GetLrPracticeItemDetailQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetPublishedByIdAsync(
            request.ItemId,
            cancellationToken);

        if (item is null)
            return null;

        var questions = item.Questions
            .OrderBy(question => question.Ordinal)
            .Select(question => new LrPracticeQuestionDto(
                question.Id,
                question.Ordinal,
                question.Code,
                question.Prompt,
                question.OptionA,
                question.OptionB,
                question.OptionC,
                question.OptionD
            ))
            .ToList();

        return new LrPracticeDetailDto(
            item.Id,
            (int)item.Part,
            item.Kind.ToString(),
            item.Code,
            item.Topic,
            item.Difficulty.ToString(),
            item.Context,
            item.AudioUrl,
            item.ImageUrl,
            questions
        );
    }
}
