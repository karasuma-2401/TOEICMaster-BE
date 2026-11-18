namespace TOEICMaster.Domain.Entities;

public enum ExamMediaKind
{
    Image = 1,
    Audio = 2
}

public sealed class ExamMediaAsset
{
    private ExamMediaAsset()
    {
    }

    private ExamMediaAsset(Guid id, string objectKey, string contentType, ExamMediaKind kind, long byteSize,
        DateTimeOffset createdAtUtc, int? durationMs)
    {
        Id = id;
        ObjectKey = objectKey;
        ContentType = contentType;
        Kind = kind;
        ByteSize = byteSize;
        CreatedAtUtc = createdAtUtc;
        DurationMs = durationMs;
    }

    public Guid Id { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public ExamMediaKind Kind { get; private set; }
    public long ByteSize { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int? DurationMs { get; private set; }

    public static ExamMediaAsset Create(string objectKey, string contentType, DateTimeOffset createdAtUtc,
        ExamMediaKind kind = ExamMediaKind.Image, long byteSize = 0, int? durationMs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegative(byteSize);
        if (durationMs is not null)
        {
            if (kind != ExamMediaKind.Audio)
                throw new ArgumentException("Duration is only supported for audio media assets.", nameof(durationMs));
            if (durationMs is <= 0 or > 3_600_000)
                throw new ArgumentOutOfRangeException(nameof(durationMs));
        }

        return new ExamMediaAsset(Guid.NewGuid(), objectKey, contentType, kind, byteSize, createdAtUtc, durationMs);
    }
}
