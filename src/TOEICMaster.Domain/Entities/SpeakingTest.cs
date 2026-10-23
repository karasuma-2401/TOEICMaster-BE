namespace TOEICMaster.Domain.Entities;

public enum SpeakingTestStatus
{
    Draft = 1,
    Published = 2
}

public sealed class SpeakingTest
{
    private SpeakingTest()
    {
    }

    private SpeakingTest(Guid id, string code, string title, string description, Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        Title = title;
        Description = description;
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
    public SpeakingTestStatus Status { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDraft => Status == SpeakingTestStatus.Draft;

    public static SpeakingTest Create(string code, string title, string description, Guid actorUserId,
        DateTimeOffset now) => new(Guid.NewGuid(), code, title, description, actorUserId, now);

    public void UpdateDraft(string code, string title, string description, Guid actorUserId, DateTimeOffset now)
    {
        if (!IsDraft)
            throw new InvalidOperationException("A published Speaking test cannot be changed.");
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

    private SpeakingTestExercise(Guid testId, int part, int sequenceNumber, Guid exerciseId)
    {
        TestId = testId;
        Part = part;
        SequenceNumber = sequenceNumber;
        ExerciseId = exerciseId;
    }

    public Guid TestId { get; private set; }
    public int Part { get; private set; }
    public int SequenceNumber { get; private set; }
    public Guid ExerciseId { get; private set; }
    public string? SnapshotJson { get; private set; }

    public static SpeakingTestExercise Create(Guid testId, int part, int sequenceNumber, Guid exerciseId)
    {
        if (!SpeakingTiming.IsPart(part))
            throw new ArgumentOutOfRangeException(nameof(part));
        if (sequenceNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        return new(testId, part, sequenceNumber, exerciseId);
    }

    public void Freeze(string snapshotJson) => SnapshotJson = snapshotJson;
}
