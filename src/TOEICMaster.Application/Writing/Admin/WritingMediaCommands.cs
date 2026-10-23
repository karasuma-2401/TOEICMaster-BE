using FluentValidation;
using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record UploadWritingImageCommand(Guid ActorUserId, byte[] Content, string ContentType,
    string OriginalFileName) : IRequest<WritingMediaAssetDto>;

public sealed record GetWritingImageQuery(Guid AssetId) : IRequest<WritingImageContent>;

public sealed class UploadWritingImageCommandValidator : AbstractValidator<UploadWritingImageCommand>
{
    public UploadWritingImageCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Content).NotEmpty().Must(content => content.Length <= UploadWritingImageCommandHandler.MaxBytes)
            .WithMessage("Ảnh không được vượt quá 5 MiB.");
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.OriginalFileName).NotEmpty().MaximumLength(255);
    }
}

public sealed class UploadWritingImageCommandHandler(IWritingAdminRepository repository, IWritingImageStore store,
    TimeProvider timeProvider) : IRequestHandler<UploadWritingImageCommand, WritingMediaAssetDto>
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<WritingMediaAssetDto> Handle(UploadWritingImageCommand request, CancellationToken cancellationToken)
    {
        var extension = DetectExtension(request.Content);
        var contentType = extension switch
        {
            "png" when request.ContentType == "image/png" => "image/png",
            "jpg" when request.ContentType == "image/jpeg" => "image/jpeg",
            _ => throw new InvalidWritingImageException()
        };
        var objectKey = $"writing/images/{Guid.NewGuid():N}.{extension}";
        var fileName = Path.GetFileName(request.OriginalFileName);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidWritingImageException();
        await store.UploadAsync(objectKey, request.Content, contentType, cancellationToken);
        var asset = WritingMediaAsset.Create(objectKey, fileName, contentType, request.Content.Length,
            request.ActorUserId, timeProvider.GetUtcNow());
        await repository.AddMediaAssetAsync(asset, cancellationToken);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try { await store.DeleteAsync(objectKey, CancellationToken.None); }
            catch { /* A storage cleanup retry can handle a rare orphan object. */ }
            throw;
        }
        return ToDto(asset);
    }

    internal static string? DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 45 &&
            bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) &&
            bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return "png";
        if (bytes.Length >= 4 && bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }) &&
            bytes.AsSpan(bytes.Length - 2).SequenceEqual(new byte[] { 0xff, 0xd9 }))
            return "jpg";
        return null;
    }

    internal static WritingMediaAssetDto ToDto(WritingMediaAsset asset) => new(asset.Id, asset.OriginalFileName,
        asset.ContentType, asset.ByteLength, asset.UploadedAtUtc, $"/api/v1/admin/writing/media/{asset.Id}");
}

public sealed class GetWritingImageQueryHandler(IWritingAdminRepository repository, IWritingImageStore store)
    : IRequestHandler<GetWritingImageQuery, WritingImageContent>
{
    public async Task<WritingImageContent> Handle(GetWritingImageQuery request, CancellationToken cancellationToken)
    {
        var asset = await repository.FindMediaAssetAsync(request.AssetId, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        return await store.DownloadAsync(asset.ObjectKey, cancellationToken);
    }
}

public sealed class InvalidWritingImageException : Exception;
