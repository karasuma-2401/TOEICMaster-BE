using MediatR;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class GetLrPracticeItemsQueryHandler(
    ILrExerciseBankRepository repository, IExamMediaStore mediaStore
) : IRequestHandler<GetLrPracticeItemsQuery, LrPracticeItemPageDto>
{
    public async Task<LrPracticeItemPageDto> Handle(
        GetLrPracticeItemsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Part is < 1 or > 7)
            throw new InvalidLrPracticeRequestException("Part must be between 1 and 7.");

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var offset = (long)(pageNumber - 1) * pageSize;
        if (offset > int.MaxValue)
            throw new InvalidLrPracticeRequestException("The requested page is out of range.");

        var (items, totalCount) = await repository.GetPublishedPageAsync(
            request.Part,
            pageNumber,
            pageSize,
            cancellationToken);

        var result = await Task.WhenAll(items.Select(async item => new LrPracticeItemDto(
            item.Id,
            (int)item.Part,
            item.Kind.ToString(),
            item.Code,
            item.Topic,
            item.Difficulty.ToString(),
            item.Context,
            await LrPracticeMediaResolver.ResolveAsync(item.AudioUrl, mediaStore, cancellationToken),
            await LrPracticeMediaResolver.ResolveAsync(item.ImageUrl, mediaStore, cancellationToken),
            item.Questions.Count
        )));

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
    ILrExerciseBankRepository repository, IExamMediaStore mediaStore
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
            await LrPracticeMediaResolver.ResolveAsync(item.AudioUrl, mediaStore, cancellationToken),
            await LrPracticeMediaResolver.ResolveAsync(item.ImageUrl, mediaStore, cancellationToken),
            questions
        );
    }
}

internal static class LrPracticeMediaResolver
{
    internal static Task<string?> ResolveAsync(string? value, IExamMediaStore store,
        CancellationToken cancellationToken) =>
        value?.StartsWith("lr-exercise/", StringComparison.Ordinal) == true
            ? ResolveStoredMediaAsync(value, store, cancellationToken)
            : Task.FromResult(value);

    private static async Task<string?> ResolveStoredMediaAsync(string key, IExamMediaStore store,
        CancellationToken cancellationToken) =>
        await store.CreatePresignedGetUrlAsync(key, TimeSpan.FromHours(2), cancellationToken);
}
