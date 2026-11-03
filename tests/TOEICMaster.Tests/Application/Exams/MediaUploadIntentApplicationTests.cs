using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.CompleteMediaUploadIntent;
using TOEICMaster.Application.Exams.Commands.CreateMediaUploadIntent;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class MediaUploadIntentApplicationTests
{
    [Fact]
    public async Task Audio_upload_is_attached_only_after_the_staged_object_is_verified()
    {
        var profile = ScoreProfile.Create("Profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-MEDIA", "Media workspace", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        revision.EnableMediaWorkspace();
        var repository = new FakeExamRepository(exam, profile);
        var store = new RecordingMediaStore(new ExamMediaObjectMetadata(8, "audio/mpeg", "etag"));

        var intent = await new CreateMediaUploadIntentCommandHandler(repository, store, TimeProvider.System).Handle(
            new CreateMediaUploadIntentCommand(exam.Id, revision.Id, ExamMediaOwnerKind.AudioGroup, "P1-Q1",
                "audio/mpeg", 8), CancellationToken.None);
        var asset = await new CompleteMediaUploadIntentCommandHandler(repository, store, TimeProvider.System).Handle(
            new CompleteMediaUploadIntentCommand(exam.Id, revision.Id, intent.IntentId), CancellationToken.None);

        Assert.StartsWith($"exam-media/{revision.Id:N}/", repository.Intent!.ObjectKey, StringComparison.Ordinal);
        Assert.Equal("https://minio.local/presigned-put", intent.UploadUrl);
        Assert.Equal(ExamMediaKind.Audio.ToString(), asset.Kind);
        Assert.Equal(asset.Id, revision.Blocks.Single(block => block.Key == "P1-Q1").AudioAssetId);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Context_image_intent_requires_an_image_context()
    {
        var profile = ScoreProfile.Create("Profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-MEDIA", "Media workspace", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        revision.EnableMediaWorkspace();
        var handler = new CreateMediaUploadIntentCommandHandler(new FakeExamRepository(exam, profile),
            new RecordingMediaStore(null), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidExamMediaException>(() => handler.Handle(
            new CreateMediaUploadIntentCommand(exam.Id, revision.Id, ExamMediaOwnerKind.ContextImage, "P5-Q101:1",
                "image/png", 8), CancellationToken.None));
    }

    private sealed class RecordingMediaStore(ExamMediaObjectMetadata? metadata) : IExamMediaStore
    {
        public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan lifetime,
            CancellationToken cancellationToken) => Task.FromResult("https://minio.local/presigned-put");
        public Task<ExamMediaObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(metadata);
    }

    private sealed class FakeExamRepository(Exam exam, ScoreProfile profile) : IExamRepository
    {
        public ExamMediaUploadIntent? Intent { get; private set; }
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
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaUploadIntentAsync(ExamMediaUploadIntent intent, CancellationToken cancellationToken)
        {
            Intent = intent;
            return Task.CompletedTask;
        }
        public Task<ExamMediaUploadIntent?> FindExamMediaUploadIntentAsync(Guid intentId,
            CancellationToken cancellationToken) => Task.FromResult(Intent?.Id == intentId ? Intent : null);
        public Task AddExamAsync(Exam value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
