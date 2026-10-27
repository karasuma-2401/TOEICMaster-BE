namespace TOEICMaster.Domain.Entities;

public sealed class ExamMediaAsset
{
    private ExamMediaAsset()
    {
    }

    private ExamMediaAsset(Guid id, string objectKey, string contentType, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ObjectKey = objectKey;
        ContentType = contentType;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ExamMediaAsset Create(string objectKey, string contentType, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return new ExamMediaAsset(Guid.NewGuid(), objectKey, contentType, createdAtUtc);
    }
}
