using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class UploadExamImageApplicationTests
{
    [Fact]
    public async Task Valid_png_is_stored_under_a_server_generated_object_key()
    {
        var repository = new FakeExamRepository();
        var store = new RecordingMediaStore();
        var handler = new UploadExamImageCommandHandler(repository, store, TimeProvider.System);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lWQAAAAASUVORK5CYII=");

        var result = await handler.Handle(new UploadExamImageCommand(png, "image/png"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("image/png", result.ContentType);
        Assert.StartsWith("exam-images/", store.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith(".png", store.ObjectKey, StringComparison.Ordinal);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Claimed_image_content_type_cannot_bypass_magic_byte_validation()
    {
        var handler = new UploadExamImageCommandHandler(new FakeExamRepository(), new RecordingMediaStore(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidExamMediaException>(() => handler.Handle(
            new UploadExamImageCommand("not an image"u8.ToArray(), "image/png"), CancellationToken.None));
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

    private sealed class FakeExamRepository : IExamRepository
    {
        public bool Saved { get; private set; }
        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) => Task.FromResult<Exam?>(null);
        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));
        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult<ScoreProfile?>(null);
        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task AddExamAsync(Exam exam, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
