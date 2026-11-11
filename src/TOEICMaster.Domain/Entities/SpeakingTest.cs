namespace TOEICMaster.Domain.Entities;

public enum SpeakingTestStatus
{
    Draft = 1,
    Published = 2
}

/// <summary>
/// Legacy tests reference the reusable exercise bank. New tests own their question content, so it is never added
/// to the practice bank or reusable by another test.
/// </summary>
public enum SpeakingTestContentMode
{
    ExerciseBank = 1,
    Direct = 2
}

public sealed class SpeakingTest
{
    private SpeakingTest()
    {
    }

    private SpeakingTest(Guid id, string code, string title, string description, SpeakingTestContentMode contentMode,
        Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        Title = title;
        Description = description;
        ContentMode = contentMode;
        Status = SpeakingTestStatus.Draft;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public SpeakingTestContentMode ContentMode { get; private set; }
    public SpeakingTestStatus Status { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDraft => Status == SpeakingTestStatus.Draft;

    public static SpeakingTest Create(string code, string title, string description, SpeakingTestContentMode contentMode,
        Guid actorUserId, DateTimeOffset now) => new(Guid.NewGuid(), code, title, description, contentMode,
        actorUserId, now);

    public void UpdateDraft(string code, string title, string description, Guid actorUserId, DateTimeOffset now)
    {
        if (!IsDraft)
            throw new InvalidOperationException("A published Speaking test cannot be changed.");
        Code = code;
        Title = title;
        Description = description;
        Touch(actorUserId, now);
    }

    /// <summary>Legacy published tests retain their frozen content but may correct their basic catalogue metadata.</summary>
    public void UpdateLegacyMetadata(string code, string title, string description, Guid actorUserId, DateTimeOffset now)
    {
        if (ContentMode != SpeakingTestContentMode.ExerciseBank)
            throw new InvalidOperationException("Only legacy Speaking tests can update metadata after publishing.");
        Code = code;
        Title = title;
        Description = description;
        Touch(actorUserId, now);
    }

    public void Publish(Guid actorUserId, DateTimeOffset now)
    {
        if (!IsDraft)
            throw new InvalidOperationException("A published Speaking test cannot be published again.");
        Status = SpeakingTestStatus.Published;
        PublishedAtUtc = now;
        Touch(actorUserId, now);
    }

    private void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }
}

/// <summary>One exercise slot of a test; <see cref="SnapshotJson"/> freezes the exercise when the test is published.</summary>
public sealed class SpeakingTestExercise
{
    private SpeakingTestExercise()
    {
    }

    private SpeakingTestExercise(Guid testId, int part, int sequenceNumber, Guid? exerciseId, string? contentJson,
        Guid? imageAssetId)
    {
        TestId = testId;
        Part = part;
        SequenceNumber = sequenceNumber;
        ExerciseId = exerciseId;
        ContentJson = contentJson;
        ImageAssetId = imageAssetId;
    }

    public Guid TestId { get; private set; }
    public int Part { get; private set; }
    public int SequenceNumber { get; private set; }
    /// <summary>Set only for legacy exercise-bank tests.</summary>
    public Guid? ExerciseId { get; private set; }
    /// <summary>Private direct-authoring content for a new test; never appears in the exercise bank.</summary>
    public string? ContentJson { get; private set; }
    /// <summary>Mirrors the Part 2 image inside <see cref="ContentJson"/> so the database can enforce its reference.</summary>
    public Guid? ImageAssetId { get; private set; }
    public string? SnapshotJson { get; private set; }

    public static SpeakingTestExercise Create(Guid testId, int part, int sequenceNumber, Guid exerciseId)
    {
        if (!SpeakingTiming.IsPart(part))
            throw new ArgumentOutOfRangeException(nameof(part));
        if (sequenceNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        return new(testId, part, sequenceNumber, exerciseId, null, null);
    }

    public static SpeakingTestExercise CreateDirect(Guid testId, int part, int sequenceNumber, string contentJson,
        Guid? imageAssetId)
    {
        if (!SpeakingTiming.IsPart(part))
            throw new ArgumentOutOfRangeException(nameof(part));
        if (sequenceNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new ArgumentException("Direct test content is required.", nameof(contentJson));
        return new(testId, part, sequenceNumber, null, contentJson, imageAssetId);
    }

    public void Freeze(string snapshotJson) => SnapshotJson = snapshotJson;
}
