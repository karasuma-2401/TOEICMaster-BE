namespace TOEICMaster.Domain.Entities;

public sealed class WritingMediaAsset
{
    private WritingMediaAsset()
    {
    }

    private WritingMediaAsset(Guid id, string objectKey, string originalFileName, string contentType, long byteLength,
        Guid uploadedByUserId, DateTimeOffset uploadedAtUtc)
    {
        Id = id;
        ObjectKey = objectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        ByteLength = byteLength;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static WritingMediaAsset Create(string objectKey, string originalFileName, string contentType,
        long byteLength, Guid uploadedByUserId, DateTimeOffset uploadedAtUtc) =>
        new(Guid.NewGuid(), objectKey, originalFileName, contentType, byteLength, uploadedByUserId, uploadedAtUtc);
}
