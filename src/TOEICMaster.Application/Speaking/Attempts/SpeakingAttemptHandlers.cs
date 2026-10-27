using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Attempts;

public sealed record GetPublishedSpeakingTestsQuery(string? Query, int PageNumber = 1, int PageSize = 20)
    : IRequest<SpeakingPageDto<SpeakingPublicTestDto>>;

public sealed record GetPublishedSpeakingTestQuery(Guid TestId) : IRequest<SpeakingPublicTestDto>;

public sealed record GetPublishedSpeakingImageQuery(Guid AssetId) : IRequest<SpeakingMediaContent>;

public sealed record StartSpeakingAttemptCommand(SpeakingAttemptOwner Owner, StartSpeakingAttemptInput Input)
    : IRequest<SpeakingAttemptDto>;

public sealed record GetSpeakingAttemptsQuery(SpeakingAttemptOwner Owner)
    : IRequest<SpeakingPageDto<SpeakingAttemptSummaryDto>>;

public sealed record GetSpeakingAttemptQuery(SpeakingAttemptOwner Owner, Guid AttemptId) : IRequest<SpeakingAttemptDto>;

public sealed record NextSpeakingPhaseCommand(SpeakingAttemptOwner Owner, Guid AttemptId) : IRequest<SpeakingAttemptDto>;

public sealed record SubmitSpeakingAttemptCommand(SpeakingAttemptOwner Owner, Guid AttemptId)
    : IRequest<SpeakingAttemptDto>;

public sealed record UploadSpeakingRecordingCommand(SpeakingAttemptOwner Owner, Guid AttemptId, int Position,
    byte[] Content, string? ContentType, int DurationSeconds) : IRequest<SpeakingAttemptDto>;

public sealed record GetSpeakingRecordingQuery(SpeakingAttemptOwner Owner, Guid AttemptId, int Position)
    : IRequest<SpeakingMediaContent>;

public sealed class GetPublishedSpeakingTestsQueryValidator : AbstractValidator<GetPublishedSpeakingTestsQuery>
{
    public GetPublishedSpeakingTestsQueryValidator()
    {
        RuleFor(query => query.Query).Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("Từ khóa tối đa 100 ký tự.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class StartSpeakingAttemptCommandValidator : AbstractValidator<StartSpeakingAttemptCommand>
{
    public StartSpeakingAttemptCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new SpeakingAttemptOwnerValidator());
        RuleFor(command => command.Input.TestId).NotEmpty().OverridePropertyName("TestId")
            .WithMessage("Cần chọn đề Speaking.");
        RuleFor(command => command.Input.Mode).Must(mode => SpeakingCodes.TryParseMode(mode, out _))
            .OverridePropertyName("Mode").WithMessage("Chế độ làm bài phải là FULL hoặc PART_1 đến PART_5.");
    }
}

public sealed class UploadSpeakingRecordingCommandValidator : AbstractValidator<UploadSpeakingRecordingCommand>
{
    public UploadSpeakingRecordingCommandValidator()
    {
        RuleFor(command => command.Owner).SetValidator(new SpeakingAttemptOwnerValidator());
        RuleFor(command => command.Position).GreaterThan(0);
        RuleFor(command => command.Content).NotEmpty().OverridePropertyName("File")
            .WithMessage("Bản ghi trống, không thể tải lên.");
        RuleFor(command => command.DurationSeconds).GreaterThanOrEqualTo(0)
            .WithMessage("Thời lượng bản ghi không hợp lệ.");
    }
}

public sealed class GetPublishedSpeakingTestsQueryHandler(ISpeakingAttemptRepository repository)
    : IRequestHandler<GetPublishedSpeakingTestsQuery, SpeakingPageDto<SpeakingPublicTestDto>>
{
    public async Task<SpeakingPageDto<SpeakingPublicTestDto>> Handle(GetPublishedSpeakingTestsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetPublishedTestPageAsync(request.Query, request.PageNumber, request.PageSize,
            cancellationToken);
        return new SpeakingPageDto<SpeakingPublicTestDto>(page.Items.Select(SpeakingAttemptMapping.ToPublicDto)
            .ToArray(), page.PageNumber, page.PageSize, page.TotalCount);
    }
}

public sealed class GetPublishedSpeakingTestQueryHandler(ISpeakingAttemptRepository repository)
    : IRequestHandler<GetPublishedSpeakingTestQuery, SpeakingPublicTestDto>
{
    public async Task<SpeakingPublicTestDto> Handle(GetPublishedSpeakingTestQuery request,
        CancellationToken cancellationToken) =>
        (await repository.FindPublishedTestAsync(request.TestId, cancellationToken))?.ToPublicDto()
        ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
}

public sealed class GetPublishedSpeakingImageQueryHandler(ISpeakingAttemptRepository repository,
    ISpeakingMediaStore store) : IRequestHandler<GetPublishedSpeakingImageQuery, SpeakingMediaContent>
{
    public async Task<SpeakingMediaContent> Handle(GetPublishedSpeakingImageQuery request,
        CancellationToken cancellationToken)
    {
        var asset = await repository.FindPublishedImageAsync(request.AssetId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy ảnh.");
        return new SpeakingMediaContent(await store.DownloadAsync(asset.Kind, asset.ObjectKey, cancellationToken),
            asset.ContentType);
    }
}

public sealed class StartSpeakingAttemptCommandHandler(ISpeakingAttemptRepository repository,
    TimeProvider timeProvider) : IRequestHandler<StartSpeakingAttemptCommand, SpeakingAttemptDto>
{
    public async Task<SpeakingAttemptDto> Handle(StartSpeakingAttemptCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await repository.FindPublishedTestAsync(request.Input.TestId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
        SpeakingCodes.TryParseMode(request.Input.Mode, out var mode);
        var part = mode.Part();
        var slots = loaded.Slots.Where(slot => part is null || slot.Part == part).ToArray();
        if (slots.Length == 0 || slots.Any(slot => slot.SnapshotJson is null))
            throw new SpeakingConflictException(part is null
                ? "Đề Speaking chưa sẵn sàng để làm bài."
                : $"Đề Speaking chưa có Part {part}.");

        var attemptId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var attempt = SpeakingAttempt.Create(attemptId, loaded.Test.Id, mode, request.Owner.UserId,
            request.Owner.AnonymousSessionHash, SpeakingAttemptMapping.BuildQuestions(attemptId, slots), now);
        repository.AddAttempt(attempt);
        await repository.SaveChangesAsync(cancellationToken);
        return attempt.ToDto(new SpeakingTestHeader(loaded.Test.Code, loaded.Test.Title), now);
    }
}

public sealed class GetSpeakingAttemptsQueryHandler(ISpeakingAttemptRepository repository)
    : IRequestHandler<GetSpeakingAttemptsQuery, SpeakingPageDto<SpeakingAttemptSummaryDto>>
{
    public const int MaxItems = 100;

    public async Task<SpeakingPageDto<SpeakingAttemptSummaryDto>> Handle(GetSpeakingAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        var attempts = await repository.GetAttemptsAsync(request.Owner, MaxItems, cancellationToken);
        var headers = await repository.GetTestHeadersAsync(attempts.Select(attempt => attempt.TestId).Distinct()
            .ToArray(), cancellationToken);
        var items = attempts.Select(attempt => attempt.ToSummaryDto(headers.GetValueOrDefault(attempt.TestId)
            ?? new SpeakingTestHeader(string.Empty, string.Empty))).ToArray();
        return new SpeakingPageDto<SpeakingAttemptSummaryDto>(items, 1, MaxItems, items.Length);
    }
}

public sealed class GetSpeakingAttemptQueryHandler(ISpeakingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetSpeakingAttemptQuery, SpeakingAttemptDto>
{
    public async Task<SpeakingAttemptDto> Handle(GetSpeakingAttemptQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await SpeakingAttemptAccess.FindOwnedAsync(repository, request.Owner, request.AttemptId,
            cancellationToken);
        if (attempt.AdvanceExpired(now))
        {
            try
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (SpeakingConcurrencyException)
            {
                // Another request advanced the same deadlines first; advancing is deterministic, so reload and
                // replay it in memory instead of failing a read.
                repository.ClearTracking();
                attempt = await SpeakingAttemptAccess.FindOwnedAsync(repository, request.Owner, request.AttemptId,
                    cancellationToken);
                attempt.AdvanceExpired(now);
            }
        }
        return await SpeakingAttemptAccess.ToDtoAsync(repository, attempt, now, cancellationToken);
    }
}

public sealed class NextSpeakingPhaseCommandHandler(ISpeakingAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<NextSpeakingPhaseCommand, SpeakingAttemptDto>
{
    /// <summary>Not retried on a conflict: two tabs pressing "next" together must not skip two phases.</summary>
    public async Task<SpeakingAttemptDto> Handle(NextSpeakingPhaseCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await SpeakingAttemptAccess.FindOwnedAsync(repository, request.Owner, request.AttemptId,
            cancellationToken);
        attempt.Next(now);
        await repository.SaveChangesAsync(cancellationToken);
        return await SpeakingAttemptAccess.ToDtoAsync(repository, attempt, now, cancellationToken);
    }
}

public sealed class SubmitSpeakingAttemptCommandHandler(ISpeakingAttemptRepository repository,
    TimeProvider timeProvider) : IRequestHandler<SubmitSpeakingAttemptCommand, SpeakingAttemptDto>
{
    public async Task<SpeakingAttemptDto> Handle(SubmitSpeakingAttemptCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await SpeakingAttemptAccess.WithRetryAsync(repository, request.Owner, request.AttemptId,
            candidate => candidate.Submit(now), cancellationToken);
        return await SpeakingAttemptAccess.ToDtoAsync(repository, attempt, now, cancellationToken);
    }
}

public sealed class UploadSpeakingRecordingCommandHandler(ISpeakingAttemptRepository repository,
    ISpeakingMediaStore store, TimeProvider timeProvider)
    : IRequestHandler<UploadSpeakingRecordingCommand, SpeakingAttemptDto>
{
    public async Task<SpeakingAttemptDto> Handle(UploadSpeakingRecordingCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempt = await SpeakingAttemptAccess.FindOwnedAsync(repository, request.Owner, request.AttemptId,
            cancellationToken);
        var question = attempt.FindQuestion(request.Position)
            ?? throw new SpeakingNotFoundException("Không tìm thấy câu hỏi trong lượt làm.");
        if (request.Content.Length > SpeakingMediaRules.MaxRecordingBytesFor(question.ResponseSeconds))
            throw new SpeakingMediaTooLargeException("Bản ghi vượt quá dung lượng cho phép của câu này.");
        var contentType = DetectRecording(request.Content, request.ContentType);
        if (request.DurationSeconds > question.ResponseSeconds + 2)
            throw new ValidationException([new ValidationFailure("DurationSeconds",
                $"Bản ghi không được dài hơn {question.ResponseSeconds} giây.")]);
        attempt.AdvanceExpired(now);
        if (!attempt.CanAcceptRecording(request.Position, now))
            throw new SpeakingConflictException("Đã hết thời gian tải bản ghi cho câu này.");

        var objectKey = $"recordings/{attempt.Id:N}/{request.Position}-{Guid.NewGuid():N}." +
                        SpeakingMediaRules.Extension(contentType);
        await store.UploadAsync(SpeakingMediaKind.Recording, objectKey, request.Content, contentType,
            cancellationToken);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(request.Content));
        SpeakingMediaAsset? replaced = null;
        try
        {
            attempt = await SpeakingAttemptAccess.WithRetryAsync(repository, request.Owner, request.AttemptId,
                async candidate =>
                {
                    var asset = SpeakingMediaAsset.Create(SpeakingMediaKind.Recording, objectKey, contentType,
                        request.Content.Length, sha256, request.Owner.UserId, now);
                    repository.AddMediaAsset(asset);
                    var previousId = candidate.AttachRecording(request.Position, asset.Id, request.DurationSeconds,
                        now);
                    replaced = previousId is { } id ? await repository.FindMediaAssetAsync(id, cancellationToken) : null;
                    if (replaced is not null)
                        repository.RemoveMediaAsset(replaced);
                    if (replaced?.ObjectKey == objectKey)
                        replaced = null;
                }, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await DeleteQuietlyAsync(objectKey);
            throw new SpeakingConflictException("Đã hết thời gian tải bản ghi cho câu này.");
        }
        catch
        {
            await DeleteQuietlyAsync(objectKey);
            throw;
        }
        if (replaced is not null)
            await DeleteQuietlyAsync(replaced.ObjectKey);
        return await SpeakingAttemptAccess.ToDtoAsync(repository, attempt, now, cancellationToken);
    }

    private static string DetectRecording(byte[] content, string? declaredContentType)
    {
        var detected = SpeakingMediaRules.DetectAudio(content);
        var declared = SpeakingMediaRules.BaseContentType(declaredContentType);
        if (detected is null || declared is not ("" or "application/octet-stream") && declared != detected)
            throw new InvalidSpeakingMediaException("Bản ghi phải là âm thanh WebM, Ogg hoặc MP4 hợp lệ.");
        return detected;
    }

    private async Task DeleteQuietlyAsync(string objectKey)
    {
        try { await store.DeleteAsync(SpeakingMediaKind.Recording, objectKey, CancellationToken.None); }
        catch { /* An orphan object is never referenced and is removed by retention cleanup. */ }
    }
}

public sealed class GetSpeakingRecordingQueryHandler(ISpeakingAttemptRepository repository, ISpeakingMediaStore store)
    : IRequestHandler<GetSpeakingRecordingQuery, SpeakingMediaContent>
{
    public async Task<SpeakingMediaContent> Handle(GetSpeakingRecordingQuery request,
        CancellationToken cancellationToken)
    {
        var attempt = await SpeakingAttemptAccess.FindOwnedAsync(repository, request.Owner, request.AttemptId,
            cancellationToken);
        var assetId = attempt.FindQuestion(request.Position)?.RecordingAssetId
            ?? throw new SpeakingNotFoundException("Câu này chưa có bản ghi.");
        var asset = await repository.FindMediaAssetAsync(assetId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Câu này chưa có bản ghi.");
        return new SpeakingMediaContent(await store.DownloadAsync(asset.Kind, asset.ObjectKey, cancellationToken),
            asset.ContentType);
    }
}

internal static class SpeakingAttemptAccess
{
    private const int MaxSaveAttempts = 3;

    /// <summary>Loads an attempt the caller owns; another owner's attempt is reported as missing.</summary>
    public static async Task<SpeakingAttempt> FindOwnedAsync(ISpeakingAttemptRepository repository,
        SpeakingAttemptOwner owner, Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(attemptId, cancellationToken);
        if (attempt is null || !attempt.IsOwnedBy(owner.UserId, owner.AnonymousSessionHash))
            throw new SpeakingNotFoundException("Không tìm thấy lượt làm Speaking.");
        return attempt;
    }

    /// <summary>Applies an idempotent change, reloading and retrying when another request saved first.</summary>
    public static Task<SpeakingAttempt> WithRetryAsync(ISpeakingAttemptRepository repository,
        SpeakingAttemptOwner owner, Guid attemptId, Action<SpeakingAttempt> change,
        CancellationToken cancellationToken) =>
        WithRetryAsync(repository, owner, attemptId, attempt =>
        {
            change(attempt);
            return Task.CompletedTask;
        }, cancellationToken);

    public static async Task<SpeakingAttempt> WithRetryAsync(ISpeakingAttemptRepository repository,
        SpeakingAttemptOwner owner, Guid attemptId, Func<SpeakingAttempt, Task> change,
        CancellationToken cancellationToken)
    {
        for (var round = 1; ; round++)
        {
            var attempt = await FindOwnedAsync(repository, owner, attemptId, cancellationToken);
            await change(attempt);
            try
            {
                await repository.SaveChangesAsync(cancellationToken);
                return attempt;
            }
            catch (SpeakingConcurrencyException) when (round < MaxSaveAttempts)
            {
                repository.ClearTracking();
            }
        }
    }

    public static async Task<SpeakingAttemptDto> ToDtoAsync(ISpeakingAttemptRepository repository,
        SpeakingAttempt attempt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var headers = await repository.GetTestHeadersAsync([attempt.TestId], cancellationToken);
        return attempt.ToDto(headers.GetValueOrDefault(attempt.TestId)
            ?? new SpeakingTestHeader(string.Empty, string.Empty), now);
    }
}
