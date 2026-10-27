namespace TOEICMaster.Domain.Entities;

public enum WritingAttemptMode
{
    Full = 1,
    Part1 = 2,
    Part2 = 3,
    Part3 = 4
}

public enum WritingAttemptStatus
{
    InProgress = 1,
    SubmittedAwaitingEvaluation = 2
}

public enum WritingAttemptPhase
{
    Preparing = 1,
    Answering = 2
}

public sealed class WritingAttempt
{
    private readonly List<WritingAttemptQuestion> _questions = [];

    private WritingAttempt()
    {
    }

    private WritingAttempt(Guid id, Guid writingTestId, string testCode, string testTitle, WritingAttemptMode mode,
        Guid? userId, string? anonymousSessionHash, DateTimeOffset now)
    {
        Id = id;
        WritingTestId = writingTestId;
        TestCode = testCode;
        TestTitle = testTitle;
        Mode = mode;
        UserId = userId;
        AnonymousSessionHash = anonymousSessionHash;
        Status = WritingAttemptStatus.InProgress;
        CurrentQuestionIndex = 0;
        CurrentPhase = WritingAttemptPhase.Preparing;
        PhaseStartedAtUtc = now;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid WritingTestId { get; private set; }
    public string TestCode { get; private set; } = string.Empty;
    public string TestTitle { get; private set; } = string.Empty;
    public WritingAttemptMode Mode { get; private set; }
    public Guid? UserId { get; private set; }
    public string? AnonymousSessionHash { get; private set; }
    public WritingAttemptStatus Status { get; private set; }
    public int CurrentQuestionIndex { get; private set; }
    public WritingAttemptPhase CurrentPhase { get; private set; }
    public DateTimeOffset PhaseStartedAtUtc { get; private set; }
    public DateTimeOffset PhaseEndsAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public IReadOnlyCollection<WritingAttemptQuestion> Questions => _questions;

    public static WritingAttempt Create(Guid id, Guid writingTestId, string testCode, string testTitle,
        WritingAttemptMode mode, Guid? userId, string? anonymousSessionHash, IReadOnlyList<WritingAttemptQuestion> questions,
        DateTimeOffset now)
    {
        if (userId is null && string.IsNullOrWhiteSpace(anonymousSessionHash))
            throw new InvalidOperationException("A Writing attempt must have an owner.");
        if (userId is not null && !string.IsNullOrWhiteSpace(anonymousSessionHash))
            throw new InvalidOperationException("A Writing attempt cannot have two owners.");
        if (questions.Count == 0)
            throw new InvalidOperationException("A Writing attempt must contain questions.");

        var attempt = new WritingAttempt(id, writingTestId, testCode, testTitle, mode, userId,
            anonymousSessionHash, now);
        attempt._questions.AddRange(questions.OrderBy(question => question.Position));
        if (attempt._questions.Select(question => question.Position).SequenceEqual(Enumerable.Range(1, questions.Count))
            is false)
            throw new InvalidOperationException("Writing attempt question positions must be continuous.");
        attempt.PhaseEndsAtUtc = now.AddSeconds(attempt._questions[0].PreparationSeconds);
        return attempt;
    }

    public bool IsOwnedBy(Guid? userId, string? anonymousSessionHash) =>
        UserId is not null
            ? userId == UserId
            : !string.IsNullOrWhiteSpace(anonymousSessionHash) && anonymousSessionHash == AnonymousSessionHash;

    public void SaveAnswer(int position, string answer, DateTimeOffset now)
    {
        AdvanceExpired(now);
        EnsureActiveAnsweringQuestion(position);
        var question = _questions[CurrentQuestionIndex];
        question.SaveAnswer(answer, now);
        UpdatedAtUtc = now;
    }

    public void Next(DateTimeOffset now)
    {
        AdvanceExpired(now);
        if (Status != WritingAttemptStatus.InProgress)
            return;
        if (CurrentPhase != WritingAttemptPhase.Answering)
            throw new InvalidOperationException("The current Writing question is still in preparation.");
        CompleteCurrentQuestion(now);
    }

    public void Submit(DateTimeOffset now)
    {
        AdvanceExpired(now);
        if (Status != WritingAttemptStatus.InProgress)
            return;
        if (CurrentQuestionIndex < _questions.Count)
            _questions[CurrentQuestionIndex].Lock(now);
        Status = WritingAttemptStatus.SubmittedAwaitingEvaluation;
        SubmittedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public bool AdvanceExpired(DateTimeOffset now)
    {
        if (Status != WritingAttemptStatus.InProgress)
            return false;

        var changed = false;
        while (Status == WritingAttemptStatus.InProgress && now >= PhaseEndsAtUtc)
        {
            var phaseEnd = PhaseEndsAtUtc;
            if (CurrentPhase == WritingAttemptPhase.Preparing)
            {
                CurrentPhase = WritingAttemptPhase.Answering;
                PhaseStartedAtUtc = phaseEnd;
                PhaseEndsAtUtc = phaseEnd.AddSeconds(_questions[CurrentQuestionIndex].ResponseSeconds);
            }
            else
            {
                CompleteCurrentQuestion(phaseEnd);
            }
            changed = true;
        }
        if (changed)
            UpdatedAtUtc = now;
        return changed;
    }

    private void EnsureActiveAnsweringQuestion(int position)
    {
        if (Status != WritingAttemptStatus.InProgress)
            throw new InvalidOperationException("This Writing attempt has already been submitted.");
        if (CurrentPhase != WritingAttemptPhase.Answering)
            throw new InvalidOperationException("The answer editor is locked during preparation.");
        if (position != CurrentQuestionIndex + 1)
            throw new InvalidOperationException("Only the current Writing question can be edited.");
    }

    private void CompleteCurrentQuestion(DateTimeOffset completedAtUtc)
    {
        _questions[CurrentQuestionIndex].Lock(completedAtUtc);
        var nextIndex = CurrentQuestionIndex + 1;
        if (nextIndex >= _questions.Count)
        {
            Status = WritingAttemptStatus.SubmittedAwaitingEvaluation;
            SubmittedAtUtc = completedAtUtc;
            UpdatedAtUtc = completedAtUtc;
            return;
        }

        CurrentQuestionIndex = nextIndex;
        CurrentPhase = WritingAttemptPhase.Preparing;
        PhaseStartedAtUtc = completedAtUtc;
        PhaseEndsAtUtc = completedAtUtc.AddSeconds(_questions[CurrentQuestionIndex].PreparationSeconds);
        UpdatedAtUtc = completedAtUtc;
    }
}

public sealed class WritingAttemptQuestion
{
    private WritingAttemptQuestion()
    {
    }

    private WritingAttemptQuestion(Guid id, Guid writingAttemptId, Guid writingTestQuestionId, int position,
        WritingPart part, int preparationSeconds, int responseSeconds, string promptSnapshotJson,
        string reviewSnapshotJson)
    {
        Id = id;
        WritingAttemptId = writingAttemptId;
        WritingTestQuestionId = writingTestQuestionId;
        Position = position;
        Part = part;
        PreparationSeconds = preparationSeconds;
        ResponseSeconds = responseSeconds;
        PromptSnapshotJson = promptSnapshotJson;
        ReviewSnapshotJson = reviewSnapshotJson;
    }

    public Guid Id { get; private set; }
    public Guid WritingAttemptId { get; private set; }
    public Guid WritingTestQuestionId { get; private set; }
    public int Position { get; private set; }
    public WritingPart Part { get; private set; }
    public int PreparationSeconds { get; private set; }
    public int ResponseSeconds { get; private set; }
    public string PromptSnapshotJson { get; private set; } = string.Empty;
    /// <summary>Private post-submission snapshot containing rubric/model answer. Never sent to a learner in progress.</summary>
    public string ReviewSnapshotJson { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;
    public DateTimeOffset? LastSavedAtUtc { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public static WritingAttemptQuestion Create(Guid writingAttemptId, Guid writingTestQuestionId, int position,
        WritingPart part, int preparationSeconds, int responseSeconds, string promptSnapshotJson) =>
        Create(writingAttemptId, writingTestQuestionId, position, part, preparationSeconds, responseSeconds,
            promptSnapshotJson, "{}");

    public static WritingAttemptQuestion Create(Guid writingAttemptId, Guid writingTestQuestionId, int position,
        WritingPart part, int preparationSeconds, int responseSeconds, string promptSnapshotJson,
        string reviewSnapshotJson) =>
        new(Guid.NewGuid(), writingAttemptId, writingTestQuestionId, position, part, preparationSeconds,
            responseSeconds, promptSnapshotJson, reviewSnapshotJson);

    public void SaveAnswer(string answer, DateTimeOffset now)
    {
        if (LockedAtUtc is not null)
            throw new InvalidOperationException("This Writing answer is locked.");
        Answer = answer;
        LastSavedAtUtc = now;
    }

    public void Lock(DateTimeOffset now)
    {
        if (LockedAtUtc is null)
            LockedAtUtc = now;
    }
}
