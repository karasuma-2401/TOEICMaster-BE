using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.CreateExam;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class CreateExamApplicationTests
{
    [Fact]
    public async Task Admin_can_create_a_draft_with_the_selected_score_profile()
    {
        var scoreProfile = ScoreProfile.Create("Reference profile", DateTimeOffset.UtcNow);
        var repository = new FakeExamRepository { ScoreProfile = scoreProfile };
        var handler = new CreateExamCommandHandler(repository, TimeProvider.System);

        var result = await handler.Handle(new CreateExamCommand("LR-2026-001", "Practice test 1", scoreProfile.Id),
            CancellationToken.None);

        var exam = Assert.Single(repository.AddedExams);
        var revision = Assert.Single(exam.Revisions);
        Assert.Equal(exam.Id, result.ExamId);
        Assert.Equal(revision.Id, result.RevisionId);
        Assert.Equal(ExamRevisionStatus.Draft, result.Status);
        Assert.Equal(200, result.QuestionCount);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Admin_cannot_create_a_draft_with_an_unknown_score_profile()
    {
        var repository = new FakeExamRepository();
        var handler = new CreateExamCommandHandler(repository, TimeProvider.System);

        await Assert.ThrowsAsync<ExamNotFoundException>(() => handler.Handle(
            new CreateExamCommand("LR-2026-001", "Practice test 1", Guid.NewGuid()), CancellationToken.None));
        Assert.Empty(repository.AddedExams);
    }

    private sealed class FakeExamRepository : IExamRepository
    {
        public ScoreProfile? ScoreProfile { get; init; }
        public List<Exam> AddedExams { get; } = [];
        public bool Saved { get; private set; }

        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult(ScoreProfile?.Id == scoreProfileId ? ScoreProfile : null);

        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
            Task.FromResult<Exam?>(null);

        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));

        public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
        {
            AddedExams.Add(exam);
            return Task.CompletedTask;
        }

        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
