namespace TOEICMaster.Domain.Entities;

public enum WritingTestStatus
{
    Draft = 1,
    Published = 2
}

public sealed class WritingTest
{
    private readonly List<WritingTestQuestion> _questions = [];

    private WritingTest()
    {
    }

    private WritingTest(Guid id, string code, string title, string description, Guid actorUserId,
        DateTimeOffset now)
    {
        Id = id;
        Code = code;
        Title = title;
        Description = description;
        Status = WritingTestStatus.Draft;
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
    public WritingTestStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<WritingTestQuestion> Questions => _questions;

    public static WritingTest Create(string code, string title, string description, Guid actorUserId,
        DateTimeOffset now) => new(Guid.NewGuid(), code, title, description, actorUserId, now);

    public bool IsDraft => Status == WritingTestStatus.Draft;

    public void SetInitialQuestions(IEnumerable<WritingTestQuestion> questions)
    {
        if (!IsDraft || Version != 1 || _questions.Count != 0)
            throw new InvalidOperationException("Initial questions can only be set when creating a draft Writing test.");
        _questions.AddRange(questions);
    }

    public void UpdateDraft(string code, string title, string description, IEnumerable<WritingTestQuestion> questions,
        Guid actorUserId, DateTimeOffset now)
    {
        if (!IsDraft)
            throw new InvalidOperationException("A published Writing test cannot be changed.");
        Code = code;
        Title = title;
        Description = description;
        _questions.Clear();
        _questions.AddRange(questions);
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }

    public void Publish(DateTimeOffset now, Guid actorUserId)
    {
        if (!IsDraft)
            throw new InvalidOperationException("A published Writing test cannot be published again.");
        Status = WritingTestStatus.Published;
        PublishedAtUtc = now;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }
}

public sealed class WritingTestQuestion
{
    private WritingTestQuestion()
    {
    }

    private WritingTestQuestion(Guid id, Guid writingTestId, Guid exerciseId, WritingPart part, int sequenceNumber)
    {
        Id = id;
        WritingTestId = writingTestId;
        ExerciseId = exerciseId;
        Part = part;
        SequenceNumber = sequenceNumber;
    }

    public Guid Id { get; private set; }
    public Guid WritingTestId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public WritingPart Part { get; private set; }
    public int SequenceNumber { get; private set; }
    public string? SnapshotJson { get; private set; }

    public static WritingTestQuestion Create(Guid writingTestId, Guid exerciseId, WritingPart part,
        int sequenceNumber) => new(Guid.NewGuid(), writingTestId, exerciseId, part, sequenceNumber);

    public void Freeze(string snapshotJson) => SnapshotJson = snapshotJson;
}
