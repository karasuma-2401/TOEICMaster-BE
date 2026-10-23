using FluentValidation;
using MediatR;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Attempts;

public sealed record GetPublishedWritingTestsQuery(string? Query, int PageNumber = 1, int PageSize = 20)
    : IRequest<WritingPublicTestPageDto>;

public sealed record GetPublishedWritingTestQuery(Guid TestId) : IRequest<WritingPublicTestDto>;

public sealed record GetWritingAttemptQuery(WritingAttemptOwner Owner, Guid AttemptId) : IRequest<WritingAttemptDto>;

public sealed record GetWritingAttemptHistoryQuery(WritingAttemptOwner Owner) : IRequest<WritingAttemptPageDto>;

public sealed record GetPublishedWritingImageQuery(Guid AssetId) : IRequest<WritingImageContent>;

public sealed class GetPublishedWritingTestsQueryValidator : AbstractValidator<GetPublishedWritingTestsQuery>
{
    public GetPublishedWritingTestsQueryValidator()
    {
        RuleFor(query => query.Query).Must(query => query is null || query.Trim().Length <= 100);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetPublishedWritingTestQueryValidator : AbstractValidator<GetPublishedWritingTestQuery>
{
    public GetPublishedWritingTestQueryValidator() => RuleFor(query => query.TestId).NotEmpty();
}

public sealed class GetPublishedWritingImageQueryValidator : AbstractValidator<GetPublishedWritingImageQuery>
{
    public GetPublishedWritingImageQueryValidator() => RuleFor(query => query.AssetId).NotEmpty();
}

public sealed class GetWritingAttemptQueryValidator : AbstractValidator<GetWritingAttemptQuery>
{
    public GetWritingAttemptQueryValidator()
    {
        RuleFor(query => query.AttemptId).NotEmpty();
        RuleFor(query => query.Owner).SetValidator(new WritingAttemptOwnerValidator());
    }
}

public sealed class GetWritingAttemptHistoryQueryValidator : AbstractValidator<GetWritingAttemptHistoryQuery>
{
    public GetWritingAttemptHistoryQueryValidator() => RuleFor(query => query.Owner).SetValidator(new WritingAttemptOwnerValidator());
}

public sealed class WritingAttemptOwnerValidator : AbstractValidator<WritingAttemptOwner>
{
    public WritingAttemptOwnerValidator() => RuleFor(owner => owner).Must(owner =>
        (owner.UserId is not null) != string.IsNullOrWhiteSpace(owner.AnonymousSessionHash));
}

public sealed class GetPublishedWritingTestsQueryHandler(IWritingAttemptRepository repository)
    : IRequestHandler<GetPublishedWritingTestsQuery, WritingPublicTestPageDto>
{
    public Task<WritingPublicTestPageDto> Handle(GetPublishedWritingTestsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetPublishedTestsAsync(request.Query, request.PageNumber, request.PageSize, cancellationToken);
}

public sealed class GetPublishedWritingTestQueryHandler(IWritingAttemptRepository repository)
    : IRequestHandler<GetPublishedWritingTestQuery, WritingPublicTestDto>
{
    public async Task<WritingPublicTestDto> Handle(GetPublishedWritingTestQuery request,
        CancellationToken cancellationToken) =>
        await repository.GetPublishedTestAsync(request.TestId, cancellationToken)
        ?? throw new WritingAttemptNotFoundException();
}

public sealed class GetPublishedWritingImageQueryHandler(IWritingAttemptRepository repository,
    IWritingImageStore store) : IRequestHandler<GetPublishedWritingImageQuery, WritingImageContent>
{
    public async Task<WritingImageContent> Handle(GetPublishedWritingImageQuery request,
        CancellationToken cancellationToken)
    {
        var asset = await repository.FindPublishedMediaAssetAsync(request.AssetId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        return await store.DownloadAsync(asset.ObjectKey, cancellationToken);
    }
}

public sealed class GetWritingAttemptQueryHandler(IWritingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetWritingAttemptQuery, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(GetWritingAttemptQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        EnsureOwner(attempt, request.Owner);
        var now = timeProvider.GetUtcNow();
        if (attempt.AdvanceExpired(now))
            await repository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(now);
    }

    internal static void EnsureOwner(WritingAttempt attempt, WritingAttemptOwner owner)
    {
        if (!attempt.IsOwnedBy(owner.UserId, owner.AnonymousSessionHash))
            throw new WritingAttemptNotFoundException();
    }
}

public sealed class GetWritingAttemptHistoryQueryHandler(IWritingAttemptRepository repository)
    : IRequestHandler<GetWritingAttemptHistoryQuery, WritingAttemptPageDto>
{
    public async Task<WritingAttemptPageDto> Handle(GetWritingAttemptHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var attempts = await repository.GetAttemptsAsync(request.Owner, cancellationToken);
        return new WritingAttemptPageDto(attempts.Select(WritingAttemptMapping.ToSummaryDto).ToArray());
    }
}
