namespace TOEICMaster.Domain.Entities;

public enum ExamMediaOwnerKind
{
    AudioGroup = 1,
    QuestionImage = 2,
    ContextImage = 3
}

public enum ExamMediaUploadIntentStatus
{
    Pending = 1,
    Completed = 2,
    Expired = 3
}

public sealed class ExamMediaUploadIntent
{
    private ExamMediaUploadIntent()
    {
    }

    private ExamMediaUploadIntent(Guid id, Guid examId, Guid revisionId, ExamMediaOwnerKind ownerKind,
        string ownerKey, string objectKey, string contentType, long expectedByteSize, DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc, int? expectedDurationMs)
    {
        Id = id;
        ExamId = examId;
        RevisionId = revisionId;
        OwnerKind = ownerKind;
        OwnerKey = ownerKey;
        ObjectKey = objectKey;
        ContentType = contentType;
        ExpectedByteSize = expectedByteSize;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        ExpectedDurationMs = expectedDurationMs;
        Status = ExamMediaUploadIntentStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid RevisionId { get; private set; }
    public ExamMediaOwnerKind OwnerKind { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedByteSize { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int? ExpectedDurationMs { get; private set; }
    public ExamMediaUploadIntentStatus Status { get; private set; }

    public static ExamMediaUploadIntent Create(Guid examId, Guid revisionId, ExamMediaOwnerKind ownerKind,
        string ownerKey, string objectKey, string contentType, long expectedByteSize, DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc, int? expectedDurationMs = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(examId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(revisionId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedByteSize);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        if (expectedDurationMs is not null)
        {
            if (ownerKind != ExamMediaOwnerKind.AudioGroup)
                throw new ArgumentException("Expected duration is only supported for audio group upload intents.",
                    nameof(expectedDurationMs));
            if (expectedDurationMs is <= 0 or > 3_600_000)
                throw new ArgumentOutOfRangeException(nameof(expectedDurationMs));
        }

        return new ExamMediaUploadIntent(Guid.NewGuid(), examId, revisionId, ownerKind, ownerKey, objectKey,
            contentType, expectedByteSize, expiresAtUtc, createdAtUtc, expectedDurationMs);
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != ExamMediaUploadIntentStatus.Pending || now > ExpiresAtUtc)
            throw new InvalidOperationException("The upload intent is no longer active.");
        Status = ExamMediaUploadIntentStatus.Completed;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == ExamMediaUploadIntentStatus.Pending && now >= ExpiresAtUtc)
            Status = ExamMediaUploadIntentStatus.Expired;
    }
}
