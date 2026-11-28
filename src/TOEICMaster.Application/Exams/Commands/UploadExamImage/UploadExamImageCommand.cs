using MediatR;
using System.Buffers.Binary;
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
    public static (string Extension, string ContentType)? DetectAudio(byte[] bytes)
    {
        if (bytes.Length >= 44 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8) &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) <= bytes.Length - 8)
        {
            var hasFormat = false;
            var hasData = false;
            for (var offset = 12; offset + 8 <= bytes.Length;)
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
                if (size > bytes.Length - offset - 8) return null;
                if (bytes.AsSpan(offset, 4).SequenceEqual("fmt "u8) && size >= 16)
                    hasFormat = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 8, 2)) is 1 or 3 &&
                        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 10, 2)) > 0 &&
                        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 12, 4)) > 0;
                if (bytes.AsSpan(offset, 4).SequenceEqual("data"u8) && size > 0) hasData = true;
                offset += 8 + (int)size + (int)(size & 1);
            }
            if (hasFormat && hasData) return ("wav", "audio/wav");
        }

        var start = 0;
        if (bytes.Length >= 10 && bytes.AsSpan().StartsWith("ID3"u8))
        {
            if (((bytes[6] | bytes[7] | bytes[8] | bytes[9]) & 0x80) != 0) return null;
            start = 10 + (bytes[6] << 21) + (bytes[7] << 14) + (bytes[8] << 7) + bytes[9];
            if ((bytes[5] & 0x10) != 0) start += 10;
        }
        if (start + 128 <= bytes.Length && bytes[start] == 0xff && (bytes[start + 1] & 0xe0) == 0xe0 &&
            ((bytes[start + 1] >> 3) & 3) != 1 && ((bytes[start + 1] >> 1) & 3) != 0 &&
            ((bytes[start + 2] >> 4) & 15) is > 0 and < 15 && ((bytes[start + 2] >> 2) & 3) != 3)
            return ("mp3", "audio/mpeg");
        return null;
    }

    public static bool MatchesClaimedContentType(string claimedContentType, string detectedContentType) =>
        string.Equals(claimedContentType, detectedContentType, StringComparison.OrdinalIgnoreCase);

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
}
