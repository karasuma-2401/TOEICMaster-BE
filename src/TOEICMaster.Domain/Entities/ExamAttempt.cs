namespace TOEICMaster.Domain.Entities;

public enum ExamAttemptStatus
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3
}

public sealed class ExamAttempt
{
    public const int DefaultTimeLimitSeconds = 7200;

    private readonly List<ExamAttemptAnswer> _answers = [];

    private ExamAttempt() { }

    public ExamAttempt(
        Guid id,
        Guid userId,
        Guid revisionId,
        Guid scoreProfileId,
        DateTimeOffset startedAtUtc,
        int timeLimitSeconds = DefaultTimeLimitSeconds)
    {
        Id = id;
        UserId = userId;
        RevisionId = revisionId;
        ScoreProfileId = scoreProfileId;
        StartedAtUtc = startedAtUtc;
        TimeLimitSeconds = timeLimitSeconds;
        Status = ExamAttemptStatus.InProgress;
        CreatedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RevisionId { get; private set; }
    public Guid ScoreProfileId { get; private set; }
    public ExamAttemptStatus Status { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public int TimeLimitSeconds { get; private set; }
    public bool IsTimeExpired { get; private set; }

    /// <summary>
    /// Set when the learner moves from Listening to Reading. From then on Listening answers are locked.
    /// </summary>
    public DateTimeOffset? ReadingStartedAtUtc { get; private set; }

    public bool IsListeningLocked => ReadingStartedAtUtc.HasValue;

    public int? ListeningRawScore { get; private set; }
    public int? ReadingRawScore { get; private set; }
    public int? ListeningScaledScore { get; private set; }
    public int? ReadingScaledScore { get; private set; }
    public int? TotalScaledScore { get; private set; }

    public IReadOnlyCollection<ExamAttemptAnswer> Answers => _answers.AsReadOnly();

    public DateTimeOffset DeadlineUtc => StartedAtUtc.AddSeconds(TimeLimitSeconds);

    /// <summary>True once the server clock has passed the deadline plus an optional grace period.</summary>
    public bool IsTimeUp(DateTimeOffset now, int graceSeconds = 0) =>
        now >= DeadlineUtc.AddSeconds(graceSeconds);

    public int GetRemainingSeconds(DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.InProgress) return 0;
        var elapsed = (int)(now - StartedAtUtc).TotalSeconds;
        return Math.Max(0, TimeLimitSeconds - elapsed);
    }

    public static bool IsListeningPart(int part) => part is >= 1 and <= 4;

    /// <summary>Moves the attempt to the Reading phase. Calling it again keeps the first timestamp.</summary>
    public void StartReading(DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Cannot start Reading on a submitted or closed attempt.");

        ReadingStartedAtUtc ??= now;
    }

    public void UpsertAnswer(Guid questionId, int questionNumber, int part, string? selectedKey, bool? isFlagged, DateTimeOffset answeredAtUtc)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Cannot update answers for a submitted or closed attempt.");
        EnsureQuestionIsEditable(part);

        var existing = _answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (existing is not null)
        {
            existing.UpdateSelection(selectedKey, isFlagged, answeredAtUtc);
        }
        else
        {
            _answers.Add(new ExamAttemptAnswer(Guid.NewGuid(), Id, questionId, questionNumber, part, selectedKey, isFlagged ?? false, answeredAtUtc));
        }
    }

    public void ToggleFlag(Guid questionId, int questionNumber, int part, bool isFlagged, DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Cannot toggle flag on closed attempt.");
        EnsureQuestionIsEditable(part);

        var existing = _answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (existing is not null)
        {
            existing.SetFlag(isFlagged);
        }
        else
        {
            _answers.Add(new ExamAttemptAnswer(Guid.NewGuid(), Id, questionId, questionNumber, part, null, isFlagged, now));
        }
    }

    /// <summary>Stores a skipped question at submission so every question has an answer row.</summary>
    public void RecordBlankAnswer(Guid questionId, int questionNumber, int part, DateTimeOffset at)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Cannot update answers for a submitted or closed attempt.");
        if (_answers.Any(a => a.QuestionId == questionId)) return;

        _answers.Add(new ExamAttemptAnswer(Guid.NewGuid(), Id, questionId, questionNumber, part, null, false, at));
    }

    private void EnsureQuestionIsEditable(int part)
    {
        if (IsListeningLocked && IsListeningPart(part))
            throw new InvalidOperationException("Listening answers are locked once Reading has started.");
    }

    public void Abandon(DateTimeOffset abandonedAtUtc)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Only in-progress attempt can be abandoned.");

        Status = ExamAttemptStatus.Abandoned;
        SubmittedAtUtc = abandonedAtUtc;
    }

    /// <param name="timedOut">True when the attempt is closed because the deadline passed.</param>
    public void GradeAndComplete(
        ScoreProfile scoreProfile,
        IReadOnlyDictionary<Guid, string?> correctKeys,
        DateTimeOffset submittedAtUtc,
        bool timedOut = false)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress attempt can be graded.");

        IsTimeExpired = timedOut;

        int listeningCorrect = 0;
        int readingCorrect = 0;

        foreach (var answer in _answers)
        {
            correctKeys.TryGetValue(answer.QuestionId, out var correctKey);
            bool isCorrect = !string.IsNullOrWhiteSpace(answer.SelectedKey) &&
                             string.Equals(answer.SelectedKey.Trim(), correctKey?.Trim(), StringComparison.OrdinalIgnoreCase);

            answer.ApplyGrading(correctKey, isCorrect);

            if (isCorrect)
            {
                if (IsListeningPart(answer.Part))
                    listeningCorrect++;
                else if (answer.Part is >= 5 and <= 7)
                    readingCorrect++;
            }
        }

        ListeningRawScore = listeningCorrect;
        ReadingRawScore = readingCorrect;

        // The score profile must be complete (101 mappings per skill); an incomplete one is a data error, not
        // something to paper over with an invented scale.
        ListeningScaledScore = scoreProfile.GetScore(ExamSkill.Listening, listeningCorrect);
        ReadingScaledScore = scoreProfile.GetScore(ExamSkill.Reading, readingCorrect);
        TotalScaledScore = ListeningScaledScore + ReadingScaledScore;

        SubmittedAtUtc = submittedAtUtc;
        Status = ExamAttemptStatus.Completed;
    }
}

public sealed class ExamAttemptAnswer
{
    private ExamAttemptAnswer() { }

    public ExamAttemptAnswer(Guid id, Guid attemptId, Guid questionId, int questionNumber, int part, string? selectedKey, bool isFlagged, DateTimeOffset answeredAtUtc)
    {
        Id = id;
        AttemptId = attemptId;
        QuestionId = questionId;
        QuestionNumber = questionNumber;
        Part = part;
        SelectedKey = selectedKey;
        IsFlagged = isFlagged;
        AnsweredAtUtc = answeredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionNumber { get; private set; }
    public int Part { get; private set; }
    public string? SelectedKey { get; private set; }
    public string? CorrectKey { get; private set; }
    public bool? IsCorrect { get; private set; }
    public bool IsFlagged { get; private set; }
    public DateTimeOffset AnsweredAtUtc { get; private set; }

    public void UpdateSelection(string? selectedKey, bool? isFlagged, DateTimeOffset answeredAtUtc)
    {
        SelectedKey = selectedKey;
        if (isFlagged.HasValue) IsFlagged = isFlagged.Value;
        AnsweredAtUtc = answeredAtUtc;
    }

    public void SetFlag(bool isFlagged)
    {
        IsFlagged = isFlagged;
    }

    public void ApplyGrading(string? correctKey, bool isCorrect)
    {
        CorrectKey = correctKey;
        IsCorrect = isCorrect;
    }
}
