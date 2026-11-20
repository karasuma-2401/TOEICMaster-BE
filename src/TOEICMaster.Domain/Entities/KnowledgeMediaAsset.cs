namespace TOEICMaster.Domain.Entities;

/// <summary>Immutable image stored in private object storage for Knowledge articles.</summary>
public sealed class KnowledgeMediaAsset
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private KnowledgeMediaAsset()
    {
    }

    private KnowledgeMediaAsset(Guid id, string objectKey, string originalFileName, string contentType,
        long byteLength, string sha256, Guid uploadedByUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ObjectKey = objectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        ByteLength = byteLength;
        Sha256 = sha256;
        UploadedByUserId = uploadedByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    /// <summary>Random MinIO object key; never returned directly to clients.</summary>
    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static KnowledgeMediaAsset Create(string objectKey, string originalFileName, string contentType,
        long byteLength, string sha256, Guid uploadedByUserId, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentOutOfRangeException.ThrowIfEqual(uploadedByUserId, Guid.Empty);
        if (byteLength is <= 0 or > MaxImageBytes)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (sha256.Trim().Length != 64)
            throw new ArgumentException("SHA-256 hex digest must be 64 characters.", nameof(sha256));

        return new KnowledgeMediaAsset(Guid.NewGuid(), objectKey.Trim(), originalFileName.Trim(), contentType.Trim(),
            byteLength, sha256.Trim().ToLowerInvariant(), uploadedByUserId, createdAtUtc);
    }
}
