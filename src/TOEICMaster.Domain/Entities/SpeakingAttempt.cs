namespace TOEICMaster.Domain.Entities;

public enum SpeakingAttemptMode
{
    Full = 1,
    Part1 = 2,
    Part2 = 3,
    Part3 = 4,
    Part4 = 5,
    Part5 = 6
}

public enum SpeakingAttemptStatus
{
    InProgress = 1,
    Submitted = 2
}

public enum SpeakingPhase
{
    Reading = 1,
    Preparing = 2,
    Responding = 3
}

/// <summary>
/// Server-timed Speaking attempt. For each question: READING (when it has reading time) → PREPARING (when it has
/// preparation time) → RESPONDING → lock; after the last question the attempt is SUBMITTED.
/// </summary>
public sealed class SpeakingAttempt
{
    /// <summary>A recording may still be uploaded this long after its question locks.</summary>
    public static readonly TimeSpan RecordingGracePeriod = TimeSpan.FromSeconds(15);

    private readonly List<SpeakingAttemptQuestion> _questions = [];

    private SpeakingAttempt()
    {
    }

    private SpeakingAttempt(Guid id, Guid? testId, Guid? exerciseId, SpeakingAttemptMode mode, Guid? ownerUserId,
        string? anonymousSessionHash, DateTimeOffset now)
    {
        Id = id;
        TestId = testId;
        ExerciseId = exerciseId;
        Mode = mode;
        OwnerUserId = ownerUserId;
        AnonymousSessionHash = anonymousSessionHash;
        Status = SpeakingAttemptStatus.InProgress;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    /// <summary>The published test this attempt was started from; null for single-exercise Part practice.</summary>
    public Guid? TestId { get; private set; }
    /// <summary>The published exercise practised on its own; null for test attempts.</summary>
    public Guid? ExerciseId { get; private set; }
    public SpeakingAttemptMode Mode { get; private set; }
    public SpeakingAttemptStatus Status { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public string? AnonymousSessionHash { get; private set; }
    public int? CurrentPosition { get; private set; }
    public SpeakingPhase? CurrentPhase { get; private set; }
    public DateTimeOffset? PhaseStartedAtUtc { get; private set; }
    public DateTimeOffset? PhaseEndsAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public int Version { get; private set; }
    /// <summary>Questions ordered by position; persistence may load them in any order.</summary>
    public IReadOnlyList<SpeakingAttemptQuestion> Questions => _questions.OrderBy(question => question.Position).ToArray();

    public static SpeakingAttempt Create(Guid id, Guid testId, SpeakingAttemptMode mode, Guid? ownerUserId,
        string? anonymousSessionHash, IReadOnlyList<SpeakingAttemptQuestion> questions, DateTimeOffset now) =>
        Create(id, testId, null, mode, ownerUserId, anonymousSessionHash, questions, now);

    /// <summary>Practises one published exercise; the mode is the exercise's Part.</summary>
    public static SpeakingAttempt CreateForExercise(Guid id, Guid exerciseId, int part, Guid? ownerUserId,
        string? anonymousSessionHash, IReadOnlyList<SpeakingAttemptQuestion> questions, DateTimeOffset now)
    {
        if (part is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(part));
        return Create(id, null, exerciseId, (SpeakingAttemptMode)(part + 1), ownerUserId, anonymousSessionHash,
            questions, now);
    }

    private static SpeakingAttempt Create(Guid id, Guid? testId, Guid? exerciseId, SpeakingAttemptMode mode,
        Guid? ownerUserId, string? anonymousSessionHash, IReadOnlyList<SpeakingAttemptQuestion> questions,
        DateTimeOffset now)
    {
        if (testId is null == exerciseId is null)
            throw new InvalidOperationException("A Speaking attempt comes from exactly one test or exercise.");
        if (ownerUserId is null == string.IsNullOrWhiteSpace(anonymousSessionHash))
            throw new InvalidOperationException("A Speaking attempt must have exactly one owner.");
        if (questions.Count == 0)
            throw new InvalidOperationException("A Speaking attempt must contain questions.");

        var attempt = new SpeakingAttempt(id, testId, exerciseId, mode, ownerUserId, anonymousSessionHash, now);
        attempt._questions.AddRange(questions.OrderBy(question => question.Position));
        if (!attempt._questions.Select(question => question.Position)
                .SequenceEqual(Enumerable.Range(1, attempt._questions.Count)))
            throw new InvalidOperationException("Speaking attempt question positions must be continuous.");
        attempt.EnterQuestion(1, now);
        return attempt;
    }

    public bool IsOwnedBy(Guid? userId, string? anonymousSessionHash) =>
        OwnerUserId is not null
            ? userId == OwnerUserId
            : !string.IsNullOrWhiteSpace(anonymousSessionHash) && anonymousSessionHash == AnonymousSessionHash;

    public SpeakingAttemptQuestion? FindQuestion(int position) =>
        _questions.SingleOrDefault(question => question.Position == position);

    /// <summary>Walks through every phase whose deadline has passed, even after a long absence.</summary>
    public bool AdvanceExpired(DateTimeOffset now)
    {
        var changed = false;
        while (Status == SpeakingAttemptStatus.InProgress && PhaseEndsAtUtc is { } endsAt && now >= endsAt)
        {
            EndCurrentPhase(endsAt);
            changed = true;
        }
        if (changed)
            Touch(now);
        return changed;
    }

    /// <summary>Ends the current phase now; during RESPONDING of the last question this submits the attempt.</summary>
    public void Next(DateTimeOffset now)
    {
        AdvanceExpired(now);
        if (Status != SpeakingAttemptStatus.InProgress)
            return;
        EndCurrentPhase(now);
        Touch(now);
    }

    /// <summary>Submits early; idempotent once the attempt is submitted.</summary>
    public void Submit(DateTimeOffset now)
    {
        AdvanceExpired(now);
        if (Status != SpeakingAttemptStatus.InProgress)
            return;
        QuestionAt(CurrentPosition!.Value).Lock(now);
        Finish(now);
        Touch(now);
    }

    public bool CanAcceptRecording(int position, DateTimeOffset now)
    {
        var question = FindQuestion(position);
        if (question is null)
            return false;
        if (Status == SpeakingAttemptStatus.InProgress && CurrentPosition == position &&
            CurrentPhase == SpeakingPhase.Responding)
            return true;
        return question.LockedAtUtc is { } lockedAt && now <= lockedAt + RecordingGracePeriod;
    }

    /// <summary>Attaches a recording and returns the replaced asset id, if any.</summary>
    public Guid? AttachRecording(int position, Guid assetId, int durationSeconds, DateTimeOffset now)
    {
        AdvanceExpired(now);
        if (!CanAcceptRecording(position, now))
            throw new InvalidOperationException("The recording window for this Speaking question has closed.");
        var previous = QuestionAt(position).AttachRecording(assetId, durationSeconds, now);
        Touch(now);
        return previous;
    }

    private void EndCurrentPhase(DateTimeOffset at)
    {
        var position = CurrentPosition!.Value;
        var question = QuestionAt(position);
        switch (CurrentPhase)
        {
            case SpeakingPhase.Reading:
                StartPhase(question.PreparationSeconds > 0 ? SpeakingPhase.Preparing : SpeakingPhase.Responding,
                    question, at);
                break;
            case SpeakingPhase.Preparing:
                StartPhase(SpeakingPhase.Responding, question, at);
                break;
            case SpeakingPhase.Responding:
                question.Lock(at);
                if (position < _questions.Count)
                    EnterQuestion(position + 1, at);
                else
                    Finish(at);
                break;
            default:
                throw new InvalidOperationException("The Speaking attempt has no active phase.");
        }
    }

    private SpeakingAttemptQuestion QuestionAt(int position) =>
        _questions.Single(question => question.Position == position);

    private void EnterQuestion(int position, DateTimeOffset at)
    {
        var question = QuestionAt(position);
        CurrentPosition = position;
        StartPhase(question.ReadingSeconds > 0
            ? SpeakingPhase.Reading
            : question.PreparationSeconds > 0
                ? SpeakingPhase.Preparing
                : SpeakingPhase.Responding, question, at);
    }

    private void StartPhase(SpeakingPhase phase, SpeakingAttemptQuestion question, DateTimeOffset at)
    {
        CurrentPhase = phase;
        PhaseStartedAtUtc = at;
        PhaseEndsAtUtc = at.AddSeconds(phase switch
        {
            SpeakingPhase.Reading => question.ReadingSeconds,
            SpeakingPhase.Preparing => question.PreparationSeconds,
            _ => question.ResponseSeconds
        });
    }

    private void Finish(DateTimeOffset at)
    {
        Status = SpeakingAttemptStatus.Submitted;
        SubmittedAtUtc = at;
        CurrentPosition = null;
        CurrentPhase = null;
        PhaseStartedAtUtc = null;
        PhaseEndsAtUtc = null;
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        Version++;
    }
}

public sealed class SpeakingAttemptQuestion
{
    private SpeakingAttemptQuestion()
    {
    }

    private SpeakingAttemptQuestion(Guid attemptId, int position, int part, int questionIndex, Guid exerciseId,
        string contentJson, string reviewJson, SpeakingQuestionTiming timing)
    {
        AttemptId = attemptId;
        Position = position;
        Part = part;
        QuestionIndex = questionIndex;
        ExerciseId = exerciseId;
        ContentJson = contentJson;
        ReviewJson = reviewJson;
        ReadingSeconds = timing.ReadingSeconds;
        PreparationSeconds = timing.PreparationSeconds;
        ResponseSeconds = timing.ResponseSeconds;
    }

    public Guid AttemptId { get; private set; }
    public int Position { get; private set; }
    public int Part { get; private set; }
    public int QuestionIndex { get; private set; }
    public Guid ExerciseId { get; private set; }
    /// <summary>Full content snapshot; unreached Part 3/4 questions are hidden when mapped to a response.</summary>
    public string ContentJson { get; private set; } = "{}";
    /// <summary>Private rubric and model answer; only returned after submission.</summary>
    public string ReviewJson { get; private set; } = "{}";
    public int ReadingSeconds { get; private set; }
    public int PreparationSeconds { get; private set; }
    public int ResponseSeconds { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }
    public Guid? RecordingAssetId { get; private set; }
    public int? RecordingDurationSeconds { get; private set; }
    public DateTimeOffset? RecordingUploadedAtUtc { get; private set; }

    public static SpeakingAttemptQuestion Create(Guid attemptId, int position, int part, int questionIndex,
        Guid exerciseId, string contentJson, string reviewJson) =>
        new(attemptId, position, part, questionIndex, exerciseId, contentJson, reviewJson,
            SpeakingTiming.For(part, questionIndex));

    public void Lock(DateTimeOffset now) => LockedAtUtc ??= now;

    public Guid? AttachRecording(Guid assetId, int durationSeconds, DateTimeOffset now)
    {
        var previous = RecordingAssetId;
        RecordingAssetId = assetId;
        RecordingDurationSeconds = durationSeconds;
        RecordingUploadedAtUtc = now;
        return previous;
    }

    public Guid? RemoveRecording()
    {
        var previous = RecordingAssetId;
        RecordingAssetId = null;
        RecordingDurationSeconds = null;
        RecordingUploadedAtUtc = null;
        return previous;
    }
}
