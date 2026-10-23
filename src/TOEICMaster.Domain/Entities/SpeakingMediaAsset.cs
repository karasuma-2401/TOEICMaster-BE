namespace TOEICMaster.Domain.Entities;

public enum SpeakingMediaKind
{
    Image = 1,
    Recording = 2
}

/// <summary>Immutable media object; replacing a picture or recording creates a new asset.</summary>
public sealed class SpeakingMediaAsset
{
    private SpeakingMediaAsset()
    {
    }

    private SpeakingMediaAsset(Guid id, SpeakingMediaKind kind, string objectKey, string contentType, long sizeBytes,
        string sha256, Guid? createdByUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Kind = kind;
        ObjectKey = objectKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public SpeakingMediaKind Kind { get; private set; }
    /// <summary>Random MinIO key; never returned by the API.</summary>
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SpeakingMediaAsset Create(SpeakingMediaKind kind, string objectKey, string contentType,
        long sizeBytes, string sha256, Guid? createdByUserId, DateTimeOffset createdAtUtc)
    {
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        return new(Guid.NewGuid(), kind, objectKey, contentType, sizeBytes, sha256, createdByUserId, createdAtUtc);
    }
}
