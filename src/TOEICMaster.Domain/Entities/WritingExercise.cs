namespace TOEICMaster.Domain.Entities;

public enum WritingPart
{
    Part1 = 1,
    Part2 = 2,
    Part3 = 3
}

public enum WritingDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public enum WritingExerciseStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public sealed class WritingExercise
{
    private WritingExercise()
    {
    }

    private WritingExercise(Guid id, WritingPart part, string code, string topic, WritingDifficulty difficulty,
        WritingExerciseStatus status, string rubricNotes, string modelAnswer, Guid? imageAssetId, string? imageAlt,
        string? keywordOne, string? keywordTwo, string? sender, string? recipient, string? subject,
        string? emailBody, string? directions, string? prompt, int? minimumWordCount, string? outline,
        Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Part = part;
        Code = code;
        Topic = topic;
        Difficulty = difficulty;
        Status = status;
        RubricNotes = rubricNotes;
        ModelAnswer = modelAnswer;
        ImageAssetId = imageAssetId;
        ImageAlt = imageAlt;
        KeywordOne = keywordOne;
        KeywordTwo = keywordTwo;
        Sender = sender;
        Recipient = recipient;
        Subject = subject;
        EmailBody = emailBody;
        Directions = directions;
        Prompt = prompt;
        MinimumWordCount = minimumWordCount;
        Outline = outline;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public WritingPart Part { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public WritingDifficulty Difficulty { get; private set; }
    public WritingExerciseStatus Status { get; private set; }
    public string RubricNotes { get; private set; } = string.Empty;
    public string ModelAnswer { get; private set; } = string.Empty;
    public Guid? ImageAssetId { get; private set; }
    public string? ImageAlt { get; private set; }
    public string? KeywordOne { get; private set; }
    public string? KeywordTwo { get; private set; }
    public string? Sender { get; private set; }
    public string? Recipient { get; private set; }
    public string? Subject { get; private set; }
    public string? EmailBody { get; private set; }
    public string? Directions { get; private set; }
    public string? Prompt { get; private set; }
    public int? MinimumWordCount { get; private set; }
    public string? Outline { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }

    public static WritingExercise Create(WritingPart part, string code, string topic, WritingDifficulty difficulty,
        WritingExerciseStatus status, string rubricNotes, string modelAnswer, Guid? imageAssetId, string? imageAlt,
        string? keywordOne, string? keywordTwo, string? sender, string? recipient, string? subject,
        string? emailBody, string? directions, string? prompt, int? minimumWordCount, string? outline,
        Guid actorUserId, DateTimeOffset now) =>
        new(Guid.NewGuid(), part, code, topic, difficulty, status, rubricNotes, modelAnswer, imageAssetId, imageAlt,
            keywordOne, keywordTwo, sender, recipient, subject, emailBody, directions, prompt, minimumWordCount,
            outline, actorUserId, now);

    public void Update(string code, string topic, WritingDifficulty difficulty, WritingExerciseStatus status,
        string rubricNotes, string modelAnswer, Guid? imageAssetId, string? imageAlt, string? keywordOne,
        string? keywordTwo, string? sender, string? recipient, string? subject, string? emailBody,
        string? directions, string? prompt, int? minimumWordCount, string? outline, Guid actorUserId,
        DateTimeOffset now)
    {
        Code = code;
        Topic = topic;
        Difficulty = difficulty;
        Status = status;
        RubricNotes = rubricNotes;
        ModelAnswer = modelAnswer;
        ImageAssetId = imageAssetId;
        ImageAlt = imageAlt;
        KeywordOne = keywordOne;
        KeywordTwo = keywordTwo;
        Sender = sender;
        Recipient = recipient;
        Subject = subject;
        EmailBody = emailBody;
        Directions = directions;
        Prompt = prompt;
        MinimumWordCount = minimumWordCount;
        Outline = outline;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }

    public void Archive(Guid actorUserId, DateTimeOffset now)
    {
        Status = WritingExerciseStatus.Archived;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }
}
