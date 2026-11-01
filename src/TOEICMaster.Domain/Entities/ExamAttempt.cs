namespace TOEICMaster.Domain.Entities;

public enum ExamAttemptStatus
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3
}

public sealed class ExamAttempt
{
    private readonly List<ExamAttemptAnswer> _answers = [];

    private ExamAttempt() { }

    public ExamAttempt(
        Guid id,
        Guid userId,
        Guid revisionId,
        Guid scoreProfileId,
        DateTimeOffset startedAtUtc,
        int timeLimitSeconds = 7200)
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

    public int? ListeningRawScore { get; private set; }
    public int? ReadingRawScore { get; private set; }
    public int? ListeningScaledScore { get; private set; }
    public int? ReadingScaledScore { get; private set; }
    public int? TotalScaledScore { get; private set; }

    public IReadOnlyCollection<ExamAttemptAnswer> Answers => _answers.AsReadOnly();

    public int GetRemainingSeconds(DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.InProgress) return 0;
        var elapsed = (int)(now - StartedAtUtc).TotalSeconds;
        return Math.Max(0, TimeLimitSeconds - elapsed);
    }

    public void UpsertAnswer(Guid questionId, int questionNumber, int part, string? selectedKey, bool? isFlagged, DateTimeOffset answeredAtUtc)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Cannot update answers for a submitted or closed attempt.");

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

    public void Abandon(DateTimeOffset abandonedAtUtc)
    {
        if (Status != ExamAttemptStatus.InProgress)
            throw new InvalidOperationException("Only in-progress attempt can be abandoned.");

        Status = ExamAttemptStatus.Abandoned;
        SubmittedAtUtc = abandonedAtUtc;
    }

    public void GradeAndComplete(ScoreProfile scoreProfile, IReadOnlyDictionary<Guid, string?> correctKeys, DateTimeOffset submittedAtUtc)
    {
        if (Status == ExamAttemptStatus.Completed)
            throw new InvalidOperationException("Attempt is already completed.");

        var elapsedSeconds = (submittedAtUtc - StartedAtUtc).TotalSeconds;
        if (elapsedSeconds > (TimeLimitSeconds + 60))
        {
            IsTimeExpired = true;
        }

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
                if (answer.Part >= 1 && answer.Part <= 4)
                    listeningCorrect++;
                else if (answer.Part >= 5 && answer.Part <= 7)
                    readingCorrect++;
            }
        }

        ListeningRawScore = listeningCorrect;
        ReadingRawScore = readingCorrect;

        // Tra bảng điểm an toàn (fallback tính toán nếu profile thiếu mốc)
        ListeningScaledScore = SafeGetScore(scoreProfile, ExamSkill.Listening, listeningCorrect);
        ReadingScaledScore = SafeGetScore(scoreProfile, ExamSkill.Reading, readingCorrect);
        TotalScaledScore = ListeningScaledScore + ReadingScaledScore;

        SubmittedAtUtc = submittedAtUtc;
        Status = ExamAttemptStatus.Completed;
    }

    private static int SafeGetScore(ScoreProfile scoreProfile, ExamSkill skill, int rawCorrectCount)
    {
        try
        {
            return scoreProfile.GetScore(skill, rawCorrectCount);
        }
        catch
        {
            // Barem TOEIC chuẩn fallback (5 -> 495, bội số của 5)
            if (rawCorrectCount <= 0) return 5;
            if (rawCorrectCount >= 100) return 495;
            return Math.Min(495, (int)(Math.Round((rawCorrectCount * 4.95) / 5.0) * 5));
        }
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
