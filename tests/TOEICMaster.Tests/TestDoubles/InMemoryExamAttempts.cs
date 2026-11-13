using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

/// <summary>
/// In-memory exam attempt repository over a real <see cref="Exam"/> aggregate. The exam has a published revision
/// with a few filled questions: Q1 (Part 1, key A), Q7 (Part 2, A-C only, key B) and Q101 (Part 5, key C).
/// </summary>
public sealed class InMemoryExamAttempts : IExamAttemptRepository
{
    public const int PartOneQuestion = 1;
    public const int PartTwoQuestion = 7;
    public const int PartFiveQuestion = 101;

    private readonly List<ExamAttempt> _attempts = [];

    public InMemoryExamAttempts()
    {
        ScoreProfile = ScoreProfile.Create("Test profile", DateTimeOffset.UtcNow);
        foreach (var skill in new[] { ExamSkill.Listening, ExamSkill.Reading })
            for (var raw = 0; raw <= 100; raw++)
                ScoreProfile.SetScore(skill, raw, Math.Min(495, 5 + (raw * 5)));

        Exam = Exam.Create("LR-TEST-001", "Test exam", ScoreProfile.Id, DateTimeOffset.UtcNow);
        var draft = Exam.Revisions.Single();
        draft.UpdateQuestion(PartOneQuestion, Content("P1-1", "A", withOptionD: true));
        draft.UpdateQuestion(PartTwoQuestion, Content("P2-7", "B", withOptionD: false));
        draft.UpdateQuestion(PartFiveQuestion, Content("P5-101", "C", withOptionD: true));
        Exam.PublishDraft(draft.Id);
        Revision = Exam.Revisions.Single();
    }

    public Exam Exam { get; }
    public ExamRevision Revision { get; }
    public ScoreProfile ScoreProfile { get; }
    public IReadOnlyList<ExamAttempt> Attempts => _attempts;

    public Guid QuestionId(int number) =>
        Revision.Blocks.SelectMany(block => block.Questions).Single(question => question.Number == number).Id;

    private static ExamQuestionContent Content(string code, string correctKey, bool withOptionD) =>
        new(code, "Stem", "Option A", "Option B", "Option C", withOptionD ? "Option D" : null, correctKey,
            "Translation", "Explanation", null);

    public Task<IReadOnlyList<PublishedLrExamDto>> GetPublishedExamsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PublishedLrExamDto> items =
        [
            new PublishedLrExamDto(
                Exam.Id,
                Exam.Code,
                Exam.Title,
                Revision.VersionNumber,
                200,
                100,
                100,
                Revision.CreatedAtUtc)
        ];
        return Task.FromResult(items);
    }

    public Task<ExamHeader?> GetExamHeaderAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult<ExamHeader?>(examId == Exam.Id ? new ExamHeader(Exam.Id, Exam.Code, Exam.Title) : null);

    public Task<ExamRevision?> FindPublishedRevisionForExamAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult(examId == Exam.Id ? Revision : null);

    public Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken) =>
        Task.FromResult(revisionId == Revision.Id ? Revision : null);

    public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(scoreProfileId == ScoreProfile.Id ? ScoreProfile : null);

    public Task<IReadOnlyDictionary<Guid, ExamQuestionMetadata>> GetQuestionMetadataAsync(
        IReadOnlyCollection<Guid> questionIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ExamQuestionMetadata> result = Revision.Blocks
            .SelectMany(block => block.Questions.Select(question => new { block, question }))
            .Where(x => questionIds.Contains(x.question.Id))
            .ToDictionary(
                x => x.question.Id,
                x => new ExamQuestionMetadata(Revision.Id, x.question.Number, x.block.Part, x.question.OptionD is not null));
        return Task.FromResult(result);
    }

    public Task<ExamAttempt?> FindAttemptForUpdateAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_attempts.SingleOrDefault(a => a.Id == attemptId && a.UserId == userId));

    public Task<ExamAttempt?> GetAttemptAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken) =>
        FindAttemptForUpdateAsync(attemptId, userId, cancellationToken);

    public Task<Guid?> FindActiveAttemptIdAsync(Guid userId, Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult(_attempts
            .Where(a => a.UserId == userId && a.Status == ExamAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAtUtc)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefault());

    public Task<(IReadOnlyList<ExamAttemptHistoryItemDto> Items, int TotalCount)> GetAttemptHistoryAsync(
        Guid userId,
        int pageIndex,
        int pageSize,
        Guid? examId,
        CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<ExamAttemptHistoryItemDto>, int)>(([], 0));

    public void AddAttempt(ExamAttempt attempt) => _attempts.Add(attempt);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
