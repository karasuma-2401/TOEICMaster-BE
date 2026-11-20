using MediatR;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

public sealed record ExamAttemptMediaUrlDto(string Url, DateTimeOffset ExpiresAtUtc);

public sealed record GetExamAttemptMediaUrlQuery(
    Guid UserId,
    Guid AttemptId,
    ExamMediaKind Kind,
    Guid AssetId) : IRequest<ExamAttemptMediaUrlDto>;

public sealed record GetExamAttemptListeningPlaylistQuery(
    Guid UserId,
    Guid AttemptId) : IRequest<ExamAttemptListeningPlaylistDto>;

public sealed class GetExamAttemptMediaUrlHandler(
    IExamAttemptRepository repository,
    IExamMediaStore mediaStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetExamAttemptMediaUrlQuery, ExamAttemptMediaUrlDto>
{
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(3);

    public async Task<ExamAttemptMediaUrlDto> Handle(
        GetExamAttemptMediaUrlQuery request,
        CancellationToken cancellationToken)
    {
        var lookup = await repository.GetAttemptMediaAccessAsync(
            request.AttemptId,
            request.UserId,
            request.AssetId,
            request.Kind,
            cancellationToken);

        if (lookup.Status == AttemptMediaLookupStatus.ListeningLocked)
            throw new ExamAttemptConflictException("Audio phần Listening đã bị khóa sau khi chuyển sang Reading.");

        if (lookup.Status != AttemptMediaLookupStatus.Found || string.IsNullOrWhiteSpace(lookup.ObjectKey))
            throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        var url = await mediaStore.CreatePresignedGetUrlAsync(
            lookup.ObjectKey,
            UrlLifetime,
            cancellationToken);

        return new ExamAttemptMediaUrlDto(url, timeProvider.GetUtcNow().Add(UrlLifetime));
    }
}

public sealed class GetExamAttemptListeningPlaylistHandler(
    IExamAttemptRepository repository,
    IExamMediaStore mediaStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetExamAttemptListeningPlaylistQuery, ExamAttemptListeningPlaylistDto>
{
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(3);

    public async Task<ExamAttemptListeningPlaylistDto> Handle(
        GetExamAttemptListeningPlaylistQuery request,
        CancellationToken cancellationToken)
    {
        var source = await repository.GetListeningPlaylistSourceAsync(
            request.AttemptId,
            request.UserId,
            cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (source.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Danh sách audio Listening chỉ khả dụng khi bài thi đang diễn ra.");

        if (source.IsListeningLocked)
            throw new ExamAttemptConflictException("Audio phần Listening đã bị khóa sau khi chuyển sang Reading.");

        if (source.Blocks.Count == 0)
            return new ExamAttemptListeningPlaylistDto([]);

        var orderedBlocks = source.Blocks.OrderBy(block => block.FirstQuestionNumber).ToArray();
        var objectKeys = orderedBlocks.Select(block => block.ObjectKey).ToArray();
        var expiresAtUtc = timeProvider.GetUtcNow().Add(UrlLifetime);
        var urls = await mediaStore.CreatePresignedGetUrlsAsync(objectKeys, UrlLifetime, cancellationToken);

        var items = orderedBlocks
            .Select((block, index) => new ExamAttemptListeningAudioItemDto(
                block.BlockId,
                block.Part,
                block.FirstQuestionNumber,
                block.AudioAssetId,
                block.DurationMs,
                urls[index],
                expiresAtUtc))
            .ToArray();

        return new ExamAttemptListeningPlaylistDto(items);
    }
}
