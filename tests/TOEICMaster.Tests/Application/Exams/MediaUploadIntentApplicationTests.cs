using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.CompleteMediaUploadIntent;
using TOEICMaster.Application.Exams.Commands.CreateMediaUploadIntent;
using TOEICMaster.Application.Exams.Queries.GetExamRevision;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class MediaUploadIntentApplicationTests
{
    private static (Exam Exam, ExamRevision Revision, FakeExamRepository Repository) CreateDraft(
        Action<ExamRevision>? configure = null)
    {
        var profile = ScoreProfile.Create("Profile", DateTimeOffset.UtcNow);
        var exam = Exam.Create("LR-2026-MEDIA", "Media workspace", profile.Id, DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        configure?.Invoke(revision);
        return (exam, revision, new FakeExamRepository(exam, profile));
    }

    private static ISender CreateSender(IExamRepository repository, IExamMediaStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(repository);
        services.AddSingleton(store);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Audio_upload_is_attached_and_copies_expected_duration_only_after_verification()
    {
        var (exam, revision, repository) = CreateDraft();
        var store = new RecordingMediaStore(new ExamMediaObjectMetadata(8, "audio/mpeg", "etag"));

        var intent = await new CreateMediaUploadIntentCommandHandler(repository, store, TimeProvider.System).Handle(
            new CreateMediaUploadIntentCommand(exam.Id, revision.Id, ExamMediaOwnerKind.AudioGroup, "P1-Q1",
                "audio/mpeg", 8, 42_500), CancellationToken.None);
        var asset = await new CompleteMediaUploadIntentCommandHandler(repository, store, TimeProvider.System).Handle(
            new CompleteMediaUploadIntentCommand(exam.Id, revision.Id, intent.IntentId), CancellationToken.None);

        Assert.StartsWith($"exam-media/{revision.Id:N}/", repository.Intent!.ObjectKey, StringComparison.Ordinal);
        Assert.Equal(42_500, repository.Intent.ExpectedDurationMs);
        Assert.Equal("https://minio.local/presigned-put", intent.UploadUrl);
        Assert.Equal(ExamMediaKind.Audio.ToString(), asset.Kind);
        Assert.Equal(42_500, asset.DurationMs);
        Assert.Equal(42_500, Assert.Single(repository.Assets).DurationMs);
        Assert.Equal(asset.Id, revision.Blocks.Single(block => block.Key == "P1-Q1").AudioAssetId);
        Assert.True(repository.Saved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(3_600_000)]
    public async Task Audio_upload_accepts_optional_and_boundary_durations(int? durationMs)
    {
        var (exam, revision, repository) = CreateDraft();
        var store = new RecordingMediaStore(new ExamMediaObjectMetadata(8, "audio/mpeg", "etag"));
        var sender = CreateSender(repository, store);

        var intent = await sender.Send(new CreateMediaUploadIntentCommand(exam.Id, revision.Id,
            ExamMediaOwnerKind.AudioGroup, "P1-Q1", "audio/mpeg", 8, durationMs));
        var asset = await sender.Send(new CompleteMediaUploadIntentCommand(exam.Id, revision.Id, intent.IntentId));

        Assert.Equal(durationMs, repository.Intent!.ExpectedDurationMs);
        Assert.Equal(durationMs, asset.DurationMs);
    }

    [Theory]
    [InlineData("audio/mp3", "audio/mpeg")]
    [InlineData("audio/x-wav", "audio/wav")]
    public async Task Audio_upload_intent_normalizes_mime_aliases(string claimedContentType, string normalizedContentType)
    {
        var (exam, revision, repository) = CreateDraft();
        var store = new RecordingMediaStore(new ExamMediaObjectMetadata(8, normalizedContentType, "etag"));
        var sender = CreateSender(repository, store);

        var intent = await sender.Send(new CreateMediaUploadIntentCommand(exam.Id, revision.Id,
            ExamMediaOwnerKind.AudioGroup, "P1-Q1", claimedContentType, 8, 15_000));

        Assert.Equal(normalizedContentType, repository.Intent!.ContentType);
        Assert.Equal(normalizedContentType, intent.RequiredHeaders["Content-Type"]);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("7")]
    [InlineData("32")]
    [InlineData("71")]
    [InlineData("101")]
    [InlineData("131")]
    [InlineData("147")]
    public async Task Question_image_upload_intent_is_allowed_for_all_parts_1_to_7(string questionKey)
    {
        var (exam, revision, repository) = CreateDraft();
        var store = new RecordingMediaStore(new ExamMediaObjectMetadata(8, "image/png", "etag"));
        var sender = CreateSender(repository, store);

        var intent = await sender.Send(new CreateMediaUploadIntentCommand(exam.Id, revision.Id,
            ExamMediaOwnerKind.QuestionImage, questionKey, "image/png", 8));

        Assert.Equal(ExamMediaOwnerKind.QuestionImage, repository.Intent!.OwnerKind);
        Assert.Equal(questionKey, repository.Intent.OwnerKey);
        Assert.Null(repository.Intent.ExpectedDurationMs);
        Assert.Equal("image/png", intent.RequiredHeaders["Content-Type"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public async Task Audio_upload_intent_rejects_invalid_duration_bounds(int invalidDurationMs)
    {
        var (exam, revision, repository) = CreateDraft();
        var sender = CreateSender(repository, new RecordingMediaStore(null));

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateMediaUploadIntentCommand(
            exam.Id, revision.Id, ExamMediaOwnerKind.AudioGroup, "P1-Q1", "audio/mpeg", 8, invalidDurationMs)));
        Assert.Null(repository.Intent);
    }

    [Fact]
    public async Task Question_image_upload_intent_rejects_non_null_duration()
    {
        var (exam, revision, repository) = CreateDraft();
        var sender = CreateSender(repository, new RecordingMediaStore(null));

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateMediaUploadIntentCommand(
            exam.Id, revision.Id, ExamMediaOwnerKind.QuestionImage, "1", "image/png", 8, 5_000)));
        Assert.Null(repository.Intent);
    }

    [Fact]
    public async Task Context_image_upload_intent_rejects_non_null_duration()
    {
        var (exam, revision, repository) = CreateDraft(rev => rev.ReplaceContextBlocks("P7-G147",
            [new ExamContextContent(1, ExamContextType.Image, null, null, "Passage image", true)]));
        var sender = CreateSender(repository, new RecordingMediaStore(null));

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateMediaUploadIntentCommand(
            exam.Id, revision.Id, ExamMediaOwnerKind.ContextImage, "P7-G147:1", "image/png", 8, 5_000)));
        Assert.Null(repository.Intent);
    }

    [Fact]
    public async Task Context_image_intent_requires_an_image_context()
    {
        var (exam, revision, repository) = CreateDraft();
        var handler = new CreateMediaUploadIntentCommandHandler(repository,
            new RecordingMediaStore(null), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidExamMediaException>(() => handler.Handle(
            new CreateMediaUploadIntentCommand(exam.Id, revision.Id, ExamMediaOwnerKind.ContextImage, "P5-Q101:1",
                "image/png", 8), CancellationToken.None));
    }

    [Fact]
    public async Task Get_exam_revision_populates_audio_duration_ms_per_block()
    {
        var audioAsset = ExamMediaAsset.Create("exam-media/p1-q1.mp3", "audio/mpeg", DateTimeOffset.UtcNow,
            ExamMediaKind.Audio, 2048, 27_400);
        var (exam, revision, repository) = CreateDraft(rev => rev.AttachAudioAsset("P1-Q1", audioAsset.Id));
        repository.Assets.Add(audioAsset);
        var sender = CreateSender(repository, new RecordingMediaStore(null));

        var detail = await sender.Send(new GetExamRevisionQuery(exam.Id, revision.Id));

        var p1Block = Assert.Single(detail.Blocks, block => block.Key == "P1-Q1");
        var p5Block = Assert.Single(detail.Blocks, block => block.Key == "P5-Q101");
        Assert.Equal(audioAsset.Id, p1Block.AudioAssetId);
        Assert.Equal(27_400, p1Block.AudioDurationMs);
        Assert.Null(p5Block.AudioAssetId);
        Assert.Null(p5Block.AudioDurationMs);
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
        public List<ExamMediaAsset> Assets { get; } = [];
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
        public Task<IReadOnlyDictionary<Guid, int?>> GetMediaDurationsAsync(IReadOnlyCollection<Guid> assetIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, int?> result = Assets
                .Where(asset => assetIds.Contains(asset.Id))
                .ToDictionary(asset => asset.Id, asset => asset.DurationMs);
            return Task.FromResult(result);
        }
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken)
        {
            Assets.Add(asset);
            return Task.CompletedTask;
        }
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
