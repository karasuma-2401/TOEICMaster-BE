using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.PublishExamRevision;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class PublishExamRevisionApplicationTests
{
    [Fact]
    public async Task Publish_is_rejected_with_field_level_errors_when_the_draft_is_incomplete()
    {
        var profile = ScoreProfile.Create("Incomplete profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-001", "Practice test 1", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        var repository = new FakeExamRepository(exam, profile);
        var handler = new PublishExamRevisionCommandHandler(repository);

        var exception = await Assert.ThrowsAsync<ExamPublicationException>(() => handler.Handle(
            new PublishExamRevisionCommand(exam.Id, revision.Id), CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.Contains("Master Audio", StringComparison.Ordinal));
        Assert.False(repository.Saved);
        Assert.Equal(ExamRevisionStatus.Draft, revision.Status);
    }

    private sealed class FakeExamRepository(Exam exam, ScoreProfile profile) : IExamRepository
    {
        public bool Saved { get; private set; }
        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
            Task.FromResult<Exam?>(exam.Id == examId ? exam : null);
        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));
        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult<ScoreProfile?>(profile.Id == scoreProfileId ? profile : null);
        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
            Task.FromResult(true);
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
