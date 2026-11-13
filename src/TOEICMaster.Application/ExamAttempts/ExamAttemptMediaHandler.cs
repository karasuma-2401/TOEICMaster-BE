using MediatR;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

public sealed record ExamAttemptMediaUrlDto(string Url, DateTimeOffset ExpiresAtUtc);

public sealed record GetExamAttemptMediaUrlQuery(
    Guid UserId,
    Guid AttemptId,
    ExamMediaKind Kind,
    Guid? AssetId = null) : IRequest<ExamAttemptMediaUrlDto>;

public sealed class GetExamAttemptMediaUrlHandler(
    IExamAttemptRepository repository,
    IExamMediaStore mediaStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetExamAttemptMediaUrlQuery, ExamAttemptMediaUrlDto>
{
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    public async Task<ExamAttemptMediaUrlDto> Handle(
        GetExamAttemptMediaUrlQuery request,
        CancellationToken cancellationToken)
    {
        var objectKey = await repository.GetAttemptMediaObjectKeyAsync(
            request.AttemptId,
            request.UserId,
            request.AssetId,
            request.Kind,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(objectKey))
            throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        var url = await mediaStore.CreatePresignedGetUrlAsync(
            objectKey,
            UrlLifetime,
            cancellationToken);

        return new ExamAttemptMediaUrlDto(url, timeProvider.GetUtcNow().Add(UrlLifetime));
    }
}
