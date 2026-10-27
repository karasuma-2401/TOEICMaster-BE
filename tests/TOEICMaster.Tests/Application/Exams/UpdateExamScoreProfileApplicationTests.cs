using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UpdateExamScoreProfile;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class UpdateExamScoreProfileApplicationTests
{
    [Fact]
    public async Task Draft_can_select_another_existing_score_profile()
    {
        var initialProfile = ScoreProfile.Create("Initial", DateTimeOffset.UtcNow);
        var selectedProfile = ScoreProfile.Create("Selected", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-001", "Practice test 1", initialProfile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        var repository = new FakeExamRepository(exam, initialProfile, selectedProfile);
        var handler = new UpdateExamScoreProfileCommandHandler(repository);

        await handler.Handle(new UpdateExamScoreProfileCommand(exam.Id, revision.Id, selectedProfile.Id),
            CancellationToken.None);

        Assert.Equal(selectedProfile.Id, revision.ScoreProfileId);
        Assert.True(repository.Saved);
    }

    private sealed class FakeExamRepository(Exam exam, params ScoreProfile[] profiles) : IExamRepository
    {
        public bool Saved { get; private set; }
        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
            Task.FromResult<Exam?>(exam.Id == examId ? exam : null);
        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));
        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult<ScoreProfile?>(profiles.SingleOrDefault(profile => profile.Id == scoreProfileId));
        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamAsync(Exam exam, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
