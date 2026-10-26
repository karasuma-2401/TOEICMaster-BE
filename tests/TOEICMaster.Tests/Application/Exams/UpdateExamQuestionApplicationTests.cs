using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UpdateExamQuestion;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class UpdateExamQuestionApplicationTests
{
    [Fact]
    public async Task Updating_a_question_changes_only_the_requested_draft_slot()
    {
        var profile = ScoreProfile.Create("Profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-001", "Practice test 1", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        var repository = new FakeExamRepository(exam, profile);
        var handler = new UpdateExamQuestionCommandHandler(repository);
        var content = new ExamQuestionContent("LR-2026-001-007", "Where?", "A", "B", "C", null, "A", null,
            "Explanation", null);

        await handler.Handle(new UpdateExamQuestionCommand(exam.Id, revision.Id, 7, content), CancellationToken.None);

        var updated = revision.Blocks.Single(block => block.Key == "P2-Q7").Questions.Single();
        Assert.Equal("LR-2026-001-007", updated.Code);
        Assert.Null(revision.Blocks.Single(block => block.Key == "P2-Q8").Questions.Single().Code);
        Assert.True(repository.Saved);
    }

    private sealed class FakeExamRepository(Exam exam, ScoreProfile profile) : IExamRepository
    {
        public bool Saved { get; private set; }
        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult<ScoreProfile?>(profile.Id == scoreProfileId ? profile : null);
        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
            Task.FromResult<Exam?>(exam.Id == examId ? exam : null);
        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));
        public Task AddExamAsync(Exam value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
