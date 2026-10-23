namespace TOEICMaster.Domain.Entities;

public enum SpeakingDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public enum SpeakingExerciseStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public sealed class SpeakingExercise
{
    private SpeakingExercise()
    {
    }

    private SpeakingExercise(Guid id, int part, string code, string topic, SpeakingDifficulty difficulty,
        SpeakingExerciseStatus status, string contentJson, Guid? imageAssetId, string rubricNotes, string modelAnswer,
        Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Part = part;
        Code = code;
        Topic = topic;
        Difficulty = difficulty;
        Status = status;
        ContentJson = contentJson;
        ImageAssetId = imageAssetId;
        RubricNotes = rubricNotes;
        ModelAnswer = modelAnswer;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public int Part { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public SpeakingDifficulty Difficulty { get; private set; }
    public SpeakingExerciseStatus Status { get; private set; }
    /// <summary>Part-specific content (passage, picture, questions, document or prompt) as JSON.</summary>
    public string ContentJson { get; private set; } = "{}";
    public Guid? ImageAssetId { get; private set; }
    public string RubricNotes { get; private set; } = string.Empty;
    public string ModelAnswer { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SpeakingExercise Create(int part, string code, string topic, SpeakingDifficulty difficulty,
        SpeakingExerciseStatus status, string contentJson, Guid? imageAssetId, string rubricNotes, string modelAnswer,
        Guid actorUserId, DateTimeOffset now)
    {
        EnsureImageMatchesPart(part, imageAssetId);
        return new(Guid.NewGuid(), part, code, topic, difficulty, status, contentJson, imageAssetId, rubricNotes,
            modelAnswer, actorUserId, now);
    }

    public void Update(string code, string topic, SpeakingDifficulty difficulty, SpeakingExerciseStatus status,
        string contentJson, Guid? imageAssetId, string rubricNotes, string modelAnswer, Guid actorUserId,
        DateTimeOffset now)
    {
        EnsureImageMatchesPart(Part, imageAssetId);
        Code = code;
        Topic = topic;
        Difficulty = difficulty;
        Status = status;
        ContentJson = contentJson;
        ImageAssetId = imageAssetId;
        RubricNotes = rubricNotes;
        ModelAnswer = modelAnswer;
        Touch(actorUserId, now);
    }

    public void Archive(Guid actorUserId, DateTimeOffset now)
    {
        Status = SpeakingExerciseStatus.Archived;
        Touch(actorUserId, now);
    }

    private void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }

    private static void EnsureImageMatchesPart(int part, Guid? imageAssetId)
    {
        if (!SpeakingTiming.IsPart(part))
            throw new ArgumentOutOfRangeException(nameof(part));
        if ((part == 2) != imageAssetId.HasValue)
            throw new InvalidOperationException("Only Part 2 Speaking exercises have a picture.");
    }
}
