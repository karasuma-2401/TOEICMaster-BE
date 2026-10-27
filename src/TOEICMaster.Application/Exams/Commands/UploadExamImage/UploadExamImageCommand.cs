using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.UploadExamImage;

public sealed record UploadExamImageCommand(byte[] Content, string ContentType) : IRequest<ExamMediaAssetDto>;

public sealed class UploadExamImageCommandHandler(IExamRepository repository, IExamMediaStore store, TimeProvider timeProvider)
    : IRequestHandler<UploadExamImageCommand, ExamMediaAssetDto>
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<ExamMediaAssetDto> Handle(UploadExamImageCommand request, CancellationToken cancellationToken)
    {
        var media = ExamMediaInspector.DetectImage(request.Content);
        if (request.Content.Length == 0 || request.Content.Length > MaxBytes || media is null ||
            !ExamMediaInspector.MatchesClaimedContentType(request.ContentType, media.Value.ContentType))
            throw new InvalidExamMediaException();

        var objectKey = $"exam-images/{Guid.NewGuid():N}.{media.Value.Extension}";
        await store.UploadAsync(objectKey, request.Content, media.Value.ContentType, cancellationToken);
        try
        {
            var asset = ExamMediaAsset.Create(objectKey, media.Value.ContentType, timeProvider.GetUtcNow());
            await repository.AddExamMediaAssetAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return ExamMediaAssetDto.From(asset);
        }
        catch
        {
            try { await store.DeleteAsync(objectKey, cancellationToken); }
            catch { /* The DB operation failed; best-effort cleanup prevents an orphaned object. */ }
            throw;
        }
    }
}

public static class ExamMediaInspector
{
    public static bool MatchesClaimedContentType(string claimedContentType, string detectedContentType) =>
        string.Equals(claimedContentType, detectedContentType, StringComparison.OrdinalIgnoreCase) ||
        (detectedContentType == "audio/mpeg" && string.Equals(claimedContentType, "audio/mp3",
            StringComparison.OrdinalIgnoreCase)) ||
        (detectedContentType == "audio/wav" && string.Equals(claimedContentType, "audio/x-wav",
            StringComparison.OrdinalIgnoreCase));

    public static (string Extension, string ContentType)? DetectImage(byte[] bytes)
    {
        if (bytes.Length >= 45 && bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) && bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return ("png", "image/png");
        if (bytes.Length >= 4 && bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }) &&
            bytes.AsSpan(bytes.Length - 2).SequenceEqual(new byte[] { 0xff, 0xd9 }))
            return ("jpg", "image/jpeg");
        if (bytes.Length >= 20 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
            (bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8) || bytes.AsSpan(12, 4).SequenceEqual("VP8L"u8) ||
             bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8)))
            return ("webp", "image/webp");
        return null;
    }

    public static (string Extension, string ContentType)? DetectMasterAudio(byte[] bytes)
    {
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            return ("wav", "audio/wav");
        if (bytes.Length >= 3 && bytes.AsSpan(0, 3).SequenceEqual("ID3"u8) ||
            bytes.Length >= 2 && bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0)
            return ("mp3", "audio/mpeg");
        return null;
    }
}
