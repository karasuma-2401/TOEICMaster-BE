using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

public sealed record CreateSpeakingExerciseCommand(Guid ActorUserId, SpeakingExerciseInput Input)
    : IRequest<SpeakingExerciseDto>;

public sealed record UpdateSpeakingExerciseCommand(Guid ActorUserId, Guid ExerciseId, int ExpectedVersion,
    SpeakingExerciseInput Input) : IRequest<SpeakingExerciseDto>;

public sealed record ArchiveSpeakingExerciseCommand(Guid ActorUserId, Guid ExerciseId, int ExpectedVersion) : IRequest;

public sealed record UploadSpeakingImageCommand(Guid ActorUserId, byte[] Content, string? ContentType)
    : IRequest<SpeakingImageDto>;

public sealed record GetSpeakingAdminImageQuery(Guid AssetId) : IRequest<SpeakingMediaContent>;

public sealed class CreateSpeakingExerciseCommandHandler(ISpeakingAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<CreateSpeakingExerciseCommand, SpeakingExerciseDto>
{
    public async Task<SpeakingExerciseDto> Handle(CreateSpeakingExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var input = await SpeakingExerciseInputs.ValidateAsync(request.Input, cancellationToken);
        if (await repository.ExerciseCodeExistsAsync(input.Code!, null, cancellationToken))
            throw new SpeakingConflictException("Mã bài Speaking đã tồn tại.");
        await SpeakingExerciseInputs.EnsureImageExistsAsync(input, repository, cancellationToken);
        var exercise = SpeakingExercise.Create(input.Part, input.Code!, input.Topic!, input.ParseDifficulty(),
            input.ParseStatus(), input.ToContentJson(), input.ImageAssetId, input.RubricNotes ?? string.Empty,
            input.ModelAnswer!, request.ActorUserId, timeProvider.GetUtcNow());
        repository.AddExercise(exercise);
        await repository.SaveChangesAsync(cancellationToken);
        return exercise.ToDto();
    }
}

public sealed class UpdateSpeakingExerciseCommandHandler(ISpeakingAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<UpdateSpeakingExerciseCommand, SpeakingExerciseDto>
{
    public async Task<SpeakingExerciseDto> Handle(UpdateSpeakingExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var input = await SpeakingExerciseInputs.ValidateAsync(request.Input, cancellationToken);
        var exercise = await repository.FindExerciseForUpdateAsync(request.ExerciseId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy bài tập Speaking.");
        if (exercise.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Bài tập đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        if (exercise.Part != input.Part)
            throw new SpeakingConflictException("Không thể đổi Part của bài tập đã tạo.");
        if (await repository.ExerciseCodeExistsAsync(input.Code!, exercise.Id, cancellationToken))
            throw new SpeakingConflictException("Mã bài Speaking đã tồn tại.");
        await SpeakingExerciseInputs.EnsureImageExistsAsync(input, repository, cancellationToken);
        exercise.Update(input.Code!, input.Topic!, input.ParseDifficulty(), input.ParseStatus(),
            input.ToContentJson(), input.ImageAssetId, input.RubricNotes ?? string.Empty, input.ModelAnswer!,
            request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return exercise.ToDto();
    }
}

public sealed class ArchiveSpeakingExerciseCommandHandler(ISpeakingAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<ArchiveSpeakingExerciseCommand>
{
    public async Task Handle(ArchiveSpeakingExerciseCommand request, CancellationToken cancellationToken)
    {
        var exercise = await repository.FindExerciseForUpdateAsync(request.ExerciseId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy bài tập Speaking.");
        if (exercise.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Bài tập đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        exercise.Archive(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UploadSpeakingImageCommandHandler(ISpeakingAdminRepository repository, ISpeakingMediaStore store,
    TimeProvider timeProvider) : IRequestHandler<UploadSpeakingImageCommand, SpeakingImageDto>
{
    public async Task<SpeakingImageDto> Handle(UploadSpeakingImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Content.Length > SpeakingMediaRules.MaxImageBytes)
            throw new SpeakingMediaTooLargeException("Ảnh không được vượt quá 5 MiB.");
        var detected = SpeakingMediaRules.DetectImage(request.Content);
        if (request.Content.Length == 0 || detected is null ||
            detected != SpeakingMediaRules.BaseContentType(request.ContentType))
            throw new InvalidSpeakingMediaException("Ảnh phải là JPEG hoặc PNG hợp lệ, tối đa 5 MiB.");

        var objectKey = $"images/{Guid.NewGuid():N}.{SpeakingMediaRules.Extension(detected)}";
        await store.UploadAsync(SpeakingMediaKind.Image, objectKey, request.Content, detected, cancellationToken);
        var asset = SpeakingMediaAsset.Create(SpeakingMediaKind.Image, objectKey, detected, request.Content.Length,
            Convert.ToHexStringLower(SHA256.HashData(request.Content)), request.ActorUserId, timeProvider.GetUtcNow());
        repository.AddMediaAsset(asset);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try { await store.DeleteAsync(SpeakingMediaKind.Image, objectKey, CancellationToken.None); }
            catch { /* A rare orphan object is harmless; the database never references it. */ }
            throw;
        }
        return new SpeakingImageDto(asset.Id, asset.ContentType, asset.SizeBytes);
    }
}

public sealed class GetSpeakingAdminImageQueryHandler(ISpeakingAdminRepository repository, ISpeakingMediaStore store)
    : IRequestHandler<GetSpeakingAdminImageQuery, SpeakingMediaContent>
{
    public async Task<SpeakingMediaContent> Handle(GetSpeakingAdminImageQuery request,
        CancellationToken cancellationToken)
    {
        var asset = await repository.FindMediaAssetAsync(request.AssetId, cancellationToken);
        if (asset is not { Kind: SpeakingMediaKind.Image })
            throw new SpeakingNotFoundException("Không tìm thấy ảnh.");
        return new SpeakingMediaContent(await store.DownloadAsync(asset.Kind, asset.ObjectKey, cancellationToken),
            asset.ContentType);
    }
}

internal static class SpeakingExerciseInputs
{
    private static readonly SpeakingExerciseInputValidator Validator = new();

    /// <summary>Validates with unprefixed field names (e.g. <c>Passage</c>) so the editor can show them per field.</summary>
    public static async Task<SpeakingExerciseInput> ValidateAsync(SpeakingExerciseInput input,
        CancellationToken cancellationToken)
    {
        await Validator.ValidateAndThrowAsync(input, cancellationToken);
        var trimmedQuestions = input.Questions?.Select(question => question?.Trim()).ToArray();
        return input with
        {
            Code = input.Code!.Trim(),
            Topic = input.Topic!.Trim(),
            RubricNotes = input.RubricNotes?.Trim() ?? string.Empty,
            ModelAnswer = input.ModelAnswer!.Trim(),
            Passage = input.Passage?.Trim(),
            ImageAssetId = input.Part == 2 ? input.ImageAssetId : null,
            ImageAlt = input.ImageAlt?.Trim(),
            Context = input.Context?.Trim(),
            Questions = trimmedQuestions,
            DocumentTitle = input.DocumentTitle?.Trim(),
            DocumentRows = input.DocumentRows?.Select(row =>
                new SpeakingInfoRowInput(row!.Label!.Trim(), row.Value!.Trim())).ToArray(),
            CallerIntro = input.CallerIntro?.Trim() ?? string.Empty,
            Prompt = input.Prompt?.Trim()
        };
    }

    public static async Task EnsureImageExistsAsync(SpeakingExerciseInput input, ISpeakingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        if (input.Part != 2)
            return;
        var asset = await repository.FindMediaAssetAsync(input.ImageAssetId!.Value, cancellationToken);
        if (asset is not { Kind: SpeakingMediaKind.Image })
            throw new ValidationException([new ValidationFailure(nameof(SpeakingExerciseInput.ImageAssetId),
                "Ảnh đề bài không tồn tại. Hãy tải ảnh lên lại.")]);
    }

    public static SpeakingDifficulty ParseDifficulty(this SpeakingExerciseInput input) =>
        SpeakingCodes.TryParseDifficulty(input.Difficulty, out var value) ? value : throw new InvalidOperationException();

    public static SpeakingExerciseStatus ParseStatus(this SpeakingExerciseInput input) =>
        SpeakingCodes.TryParseExerciseStatus(input.Status, out var value) ? value : throw new InvalidOperationException();

    /// <summary>Keeps only the fields that belong to the exercise's Part.</summary>
    public static string ToContentJson(this SpeakingExerciseInput input) => SpeakingJson.Serialize(input.Part switch
    {
        1 => new SpeakingContentDto(1, Passage: input.Passage),
        2 => new SpeakingContentDto(2, ImageAssetId: input.ImageAssetId, ImageAlt: input.ImageAlt),
        3 => new SpeakingContentDto(3, Context: input.Context, Questions: input.Questions),
        4 => new SpeakingContentDto(4, DocumentTitle: input.DocumentTitle,
            DocumentRows: input.DocumentRows!.Select(row => new SpeakingInfoRowDto(row!.Label!, row.Value!)).ToArray(),
            CallerIntro: input.CallerIntro, Questions: input.Questions),
        5 => new SpeakingContentDto(5, Prompt: input.Prompt),
        _ => throw new ArgumentOutOfRangeException(nameof(input))
    });
}
