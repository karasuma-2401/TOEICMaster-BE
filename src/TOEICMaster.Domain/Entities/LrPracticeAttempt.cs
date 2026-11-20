
namespace TOEICMaster.Domain.Entities;

public sealed class LrPracticeAttempt
{
    private readonly List<LrPracticeAttemptAnswer> _answers = [];

    private LrPracticeAttempt()
    {
    }

    private LrPracticeAttempt(
        Guid id,
        Guid userId,
        Guid itemId,
        DateTimeOffset submittedAtUtc)
    {
        Id = id;
        UserId = userId;
        ItemId = itemId;
        SubmittedAtUtc = submittedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ItemId { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public int TotalQuestions { get; private set; }
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }

    public IReadOnlyCollection<LrPracticeAttemptAnswer> Answers =>
        _answers.AsReadOnly();

    public static LrPracticeAttempt Create(
        Guid userId,
        Guid itemId,
        IReadOnlyCollection<LrPracticeAnswerSnapshot> answers,
        DateTimeOffset submittedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(itemId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(answers);

        if (answers.Count == 0)
            throw new ArgumentException("At least one answer is required.", nameof(answers));

        if (answers.GroupBy(x => x.QuestionId).Any(x => x.Count() > 1))
            throw new ArgumentException("Duplicate question IDs are not allowed.", nameof(answers));

        var attempt = new LrPracticeAttempt(
            Guid.NewGuid(),
            userId,
            itemId,
            submittedAtUtc);

        foreach (var answer in answers)
        {
            attempt._answers.Add(LrPracticeAttemptAnswer.Create(
                attempt.Id,
                answer));
        }

        attempt.TotalQuestions = attempt._answers.Count;
        attempt.CorrectCount = attempt._answers.Count(x => x.IsCorrect);
        attempt.IncorrectCount = attempt.TotalQuestions - attempt.CorrectCount;

        return attempt;
    }
}

public sealed record LrPracticeAnswerSnapshot(
    Guid QuestionId,
    int Ordinal,
    string? SelectedKey,
    string CorrectKey,
    bool IsCorrect,
    string? Translation,
    string? Explanation);

public sealed class LrPracticeAttemptAnswer
{
    private LrPracticeAttemptAnswer()
    {
    }

    private LrPracticeAttemptAnswer(
        Guid id,
        Guid attemptId,
        Guid questionId,
        int ordinal,
        string? selectedKey,
        string correctKey,
        bool isCorrect,
        string? translation,
        string? explanation)
    {
        Id = id;
        AttemptId = attemptId;
        QuestionId = questionId;
        Ordinal = ordinal;
        SelectedKey = selectedKey;
        CorrectKey = correctKey;
        IsCorrect = isCorrect;
        Translation = translation;
        Explanation = explanation;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }

    // Snapshot identifier, intentionally without a foreign key
    // to LrExerciseBankQuestion.
    public Guid QuestionId { get; private set; }

    public int Ordinal { get; private set; }
    public string? SelectedKey { get; private set; }
    public string CorrectKey { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    public string? Translation { get; private set; }
    public string? Explanation { get; private set; }

    internal static LrPracticeAttemptAnswer Create(
        Guid attemptId,
        LrPracticeAnswerSnapshot answer)
    {
        return new LrPracticeAttemptAnswer(
            Guid.NewGuid(),
            attemptId,
            answer.QuestionId,
            answer.Ordinal,
            answer.SelectedKey,
            answer.CorrectKey,
            answer.IsCorrect,
            answer.Translation,
            answer.Explanation);
    }
}
