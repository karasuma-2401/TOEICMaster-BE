using System.Security.Cryptography;

namespace TOEICMaster.Application.Knowledge;

public sealed record ValidatedKnowledgeImage(string ContentType, string Extension, string Sha256);

public static class KnowledgeImageSignature
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public static ValidatedKnowledgeImage Validate(byte[] content)
    {
        if (content is null || content.Length == 0)
            throw new InvalidKnowledgeMediaException("Tệp hình ảnh không được để trống.");
        if (content.Length > MaxBytes)
            throw new KnowledgeMediaTooLargeException("Hình ảnh vượt quá giới hạn tối đa 5 MiB.");

        string? contentType = null;
        string? extension = null;

        if (IsPng(content))
        {
            contentType = "image/png";
            extension = ".png";
        }
        else if (IsJpeg(content))
        {
            contentType = "image/jpeg";
            extension = ".jpg";
        }
        else if (IsWebp(content))
        {
            contentType = "image/webp";
            extension = ".webp";
        }

        if (contentType is null || extension is null)
            throw new InvalidKnowledgeMediaException("Hình ảnh phải là định dạng PNG, JPEG hoặc WebP hợp lệ.");

        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new ValidatedKnowledgeImage(contentType, extension, sha256);
    }

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 &&
        bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12 &&
        bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
        bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
}
