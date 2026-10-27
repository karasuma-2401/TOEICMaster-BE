using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UploadMasterAudio;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class UploadMasterAudioApplicationTests
{
    [Fact]
    public async Task Valid_wav_replaces_the_draft_master_audio()
    {
        var profile = ScoreProfile.Create("Profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-001", "Practice test 1", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        var repository = new FakeExamRepository(exam, profile);
        var store = new RecordingMediaStore();
        var handler = new UploadMasterAudioCommandHandler(repository, store);
        var wav = "RIFF"u8.ToArray().Concat(new byte[4]).Concat("WAVEfmt "u8.ToArray()).ToArray();

        await handler.Handle(new UploadMasterAudioCommand(exam.Id, revision.Id, wav, "audio/wav"),
            CancellationToken.None);

        Assert.StartsWith($"exam-audio/{revision.Id:N}/", store.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith(".wav", store.ObjectKey, StringComparison.Ordinal);
        Assert.Equal(store.ObjectKey, revision.MasterAudioObjectKey);
        Assert.True(repository.Saved);
    }

    private sealed class RecordingMediaStore : IExamMediaStore
    {
        public string ObjectKey { get; private set; } = string.Empty;
        public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
        {
            ObjectKey = objectKey;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
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
