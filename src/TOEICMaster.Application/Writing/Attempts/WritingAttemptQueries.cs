using System.Text.Json;
using FluentValidation;
using MediatR;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Attempts;

public sealed record GetPublishedWritingTestsQuery(string? Query, int PageNumber = 1, int PageSize = 20)
    : IRequest<WritingPublicTestPageDto>;

public sealed record GetPublishedWritingTestQuery(Guid TestId) : IRequest<WritingPublicTestDto>;

public sealed record GetPublishedWritingExercisesQuery(int Part, int PageNumber, int PageSize)
    : IRequest<WritingPracticeExercisePageDto>;

public sealed class GetPublishedWritingExercisesQueryValidator : AbstractValidator<GetPublishedWritingExercisesQuery>
{
    public GetPublishedWritingExercisesQueryValidator()
    {
        RuleFor(query => query.Part).InclusiveBetween(1, 3);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetPublishedWritingExercisesQueryHandler(IWritingAttemptRepository repository)
    : IRequestHandler<GetPublishedWritingExercisesQuery, WritingPracticeExercisePageDto>
{
    public Task<WritingPracticeExercisePageDto> Handle(GetPublishedWritingExercisesQuery request,
        CancellationToken cancellationToken) => repository.GetPublishedExercisesAsync(request.Part, request.PageNumber,
        request.PageSize, cancellationToken);
}

public sealed record GetWritingAttemptQuery(WritingAttemptOwner Owner, Guid AttemptId) : IRequest<WritingAttemptDto>;

public sealed record GetWritingAttemptHistoryQuery(WritingAttemptOwner Owner) : IRequest<WritingAttemptPageDto>;

public sealed record GetPublishedWritingImageQuery(Guid AssetId) : IRequest<WritingImageContent>;
public sealed record GetWritingAttemptImageQuery(WritingAttemptOwner Owner, Guid AttemptId, Guid AssetId)
    : IRequest<WritingImageContent>;

public sealed class GetWritingAttemptImageQueryValidator : AbstractValidator<GetWritingAttemptImageQuery>
{
    public GetWritingAttemptImageQueryValidator()
    {
        RuleFor(query => query.Owner).SetValidator(new WritingAttemptOwnerValidator());
        RuleFor(query => query.AttemptId).NotEmpty();
        RuleFor(query => query.AssetId).NotEmpty();
    }
}

public sealed class GetWritingAttemptImageQueryHandler(IWritingAttemptRepository repository,
    IWritingAdminRepository adminRepository, IWritingImageStore store) : IRequestHandler<GetWritingAttemptImageQuery, WritingImageContent>
{
    public async Task<WritingImageContent> Handle(GetWritingAttemptImageQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.GetAttemptAsync(request.AttemptId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        GetWritingAttemptQueryHandler.EnsureOwner(attempt, request.Owner);
        if (!attempt.Questions.Any(question =>
            JsonSerializer.Deserialize<WritingAttemptPromptSnapshot>(question.PromptSnapshotJson)?.ImageAssetId == request.AssetId))
            throw new WritingAttemptNotFoundException();
        var asset = await adminRepository.FindMediaAssetAsync(request.AssetId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        return await store.DownloadAsync(asset.ObjectKey, cancellationToken);
    }
}

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
        owner.UserId is Guid userId
            ? userId != Guid.Empty && string.IsNullOrWhiteSpace(owner.AnonymousSessionHash)
            : !string.IsNullOrWhiteSpace(owner.AnonymousSessionHash));
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

public sealed class GetWritingAttemptQueryHandler(IWritingAttemptRepository repository,
    IWritingEvaluationRepository evaluationRepository, TimeProvider timeProvider)
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
        var evaluation = await evaluationRepository.GetByAttemptIdAsync(attempt.Id, cancellationToken);
        return attempt.ToDto(now, evaluation);
    }

    internal static void EnsureOwner(WritingAttempt attempt, WritingAttemptOwner owner)
    {
        if (!attempt.IsOwnedBy(owner.UserId, owner.AnonymousSessionHash))
            throw new WritingAttemptNotFoundException();
    }
}

public sealed class GetWritingAttemptHistoryQueryHandler(IWritingAttemptRepository repository,
    IWritingEvaluationRepository evaluationRepository)
    : IRequestHandler<GetWritingAttemptHistoryQuery, WritingAttemptPageDto>
{
    public async Task<WritingAttemptPageDto> Handle(GetWritingAttemptHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var attempts = await repository.GetAttemptsAsync(request.Owner, cancellationToken);
        var evaluations = await evaluationRepository.GetByAttemptIdsAsync(attempts.Select(attempt => attempt.Id).ToArray(),
            cancellationToken);
        return new WritingAttemptPageDto(attempts.Select(attempt => WritingAttemptMapping.ToSummaryDto(attempt,
            evaluations.GetValueOrDefault(attempt.Id))).ToArray());
    }
}
