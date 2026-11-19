using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.ExamAttempts;

public sealed class ExamAttemptAudioTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly InMemoryExamAttempts _repository = new();
    private readonly RecordingExamMediaStore _mediaStore = new();
    private readonly MutableTimeProvider _clock = new(Now);

    private ISender CreateSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IExamAttemptRepository>(_repository);
        services.AddSingleton<IExamMediaStore>(_mediaStore);
        services.AddSingleton<TimeProvider>(_clock);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private Task<ExamAttemptSheetDto> StartAttemptAsync(ISender sender) =>
        sender.Send(new StartExamAttemptCommand(UserId, _repository.Exam.Id));

    [Fact]
    public async Task Current_active_attempt_includes_audio_duration_ms_on_listening_blocks_only()
    {
        var sender = CreateSender();
        var started = await StartAttemptAsync(sender);

        var current = await sender.Send(new GetCurrentActiveAttemptQuery(UserId, _repository.Exam.Id));

        Assert.NotNull(current);
        Assert.Equal(started.AttemptId, current.AttemptId);
        Assert.Equal(18_500, current.Blocks.Single(b => b.FirstQuestionNumber == 1).AudioDurationMs);
        Assert.Equal(12_000, current.Blocks.Single(b => b.FirstQuestionNumber == 7).AudioDurationMs);
        Assert.Null(current.Blocks.Single(b => b.FirstQuestionNumber == 101).AudioDurationMs);
    }

    [Fact]
    public async Task Listening_playlist_batch_signs_ordered_part_1_to_4_audio_urls_in_one_store_call()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);

        var playlist = await sender.Send(new GetExamAttemptListeningPlaylistQuery(UserId, sheet.AttemptId));

        Assert.Equal(1, _mediaStore.BatchCallCount);
        Assert.Equal(0, _mediaStore.SingleGetCallCount);
        Assert.Equal(TimeSpan.FromHours(3), _mediaStore.LastBatchLifetime);
        Assert.Equal(["exam-media/p1-q1.mp3", "exam-media/p2-q7.mp3"], _mediaStore.LastBatchKeys);

        Assert.Equal(2, playlist.Items.Count);
        Assert.Equal(1, playlist.Items[0].Part);
        Assert.Equal(1, playlist.Items[0].FirstQuestionNumber);
        Assert.Equal(_repository.PartOneAudioAsset.Id, playlist.Items[0].AudioAssetId);
        Assert.Equal(18_500, playlist.Items[0].DurationMs);
        Assert.Equal("https://minio.local/signed/0", playlist.Items[0].Url);
        Assert.Equal(Now.AddHours(3), playlist.Items[0].ExpiresAtUtc);

        Assert.Equal(2, playlist.Items[1].Part);
        Assert.Equal(7, playlist.Items[1].FirstQuestionNumber);
        Assert.Equal(_repository.PartTwoAudioAsset.Id, playlist.Items[1].AudioAssetId);
        Assert.Equal(12_000, playlist.Items[1].DurationMs);
        Assert.Equal("https://minio.local/signed/1", playlist.Items[1].Url);
        Assert.Equal(Now.AddHours(3), playlist.Items[1].ExpiresAtUtc);
    }

    [Fact]
    public async Task Listening_playlist_returns_not_found_for_missing_or_other_users_attempt()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);

        await Assert.ThrowsAsync<ExamAttemptNotFoundException>(() =>
            sender.Send(new GetExamAttemptListeningPlaylistQuery(Guid.NewGuid(), sheet.AttemptId)));
        await Assert.ThrowsAsync<ExamAttemptNotFoundException>(() =>
            sender.Send(new GetExamAttemptListeningPlaylistQuery(UserId, Guid.NewGuid())));
    }

    [Fact]
    public async Task Listening_playlist_returns_conflict_when_listening_is_locked()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);
        await sender.Send(new StartReadingCommand(UserId, sheet.AttemptId));

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            sender.Send(new GetExamAttemptListeningPlaylistQuery(UserId, sheet.AttemptId)));
        Assert.Equal(0, _mediaStore.BatchCallCount);
    }

    [Fact]
    public async Task Listening_playlist_returns_conflict_when_attempt_is_completed()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);
        await sender.Send(new SubmitExamAttemptCommand(UserId, sheet.AttemptId));

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            sender.Send(new GetExamAttemptListeningPlaylistQuery(UserId, sheet.AttemptId)));
        Assert.Equal(0, _mediaStore.BatchCallCount);
    }

    [Fact]
    public async Task Listening_playlist_returns_conflict_when_attempt_is_abandoned()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);
        await sender.Send(new AbandonExamAttemptCommand(UserId, sheet.AttemptId));

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            sender.Send(new GetExamAttemptListeningPlaylistQuery(UserId, sheet.AttemptId)));
        Assert.Equal(0, _mediaStore.BatchCallCount);
    }

    [Fact]
    public async Task Single_audio_url_succeeds_during_listening_and_review_but_conflicts_when_listening_locked()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);
        var audioAssetId = _repository.PartOneAudioAsset.Id;

        var listeningUrl = await sender.Send(new GetExamAttemptMediaUrlQuery(UserId, sheet.AttemptId,
            ExamMediaKind.Audio, audioAssetId));
        Assert.Equal("https://minio.local/single/exam-media/p1-q1.mp3", listeningUrl.Url);
        Assert.Equal(Now.AddHours(3), listeningUrl.ExpiresAtUtc);

        await sender.Send(new StartReadingCommand(UserId, sheet.AttemptId));
        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            sender.Send(new GetExamAttemptMediaUrlQuery(UserId, sheet.AttemptId, ExamMediaKind.Audio, audioAssetId)));

        await sender.Send(new SubmitExamAttemptCommand(UserId, sheet.AttemptId));
        var reviewUrl = await sender.Send(new GetExamAttemptMediaUrlQuery(UserId, sheet.AttemptId,
            ExamMediaKind.Audio, audioAssetId));
        Assert.Equal("https://minio.local/single/exam-media/p1-q1.mp3", reviewUrl.Url);
    }

    [Fact]
    public async Task Image_url_remains_accessible_even_when_listening_is_locked()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);
        await sender.Send(new StartReadingCommand(UserId, sheet.AttemptId));

        var imageUrl = await sender.Send(new GetExamAttemptMediaUrlQuery(UserId, sheet.AttemptId,
            ExamMediaKind.Image, _repository.PartOneImageAsset.Id));

        Assert.Equal("https://minio.local/single/exam-media/q1.png", imageUrl.Url);
    }

    [Fact]
    public async Task Media_url_returns_not_found_for_other_user_or_unlinked_asset()
    {
        var sender = CreateSender();
        var sheet = await StartAttemptAsync(sender);

        await Assert.ThrowsAsync<ExamAttemptNotFoundException>(() =>
            sender.Send(new GetExamAttemptMediaUrlQuery(Guid.NewGuid(), sheet.AttemptId, ExamMediaKind.Audio,
                _repository.PartOneAudioAsset.Id)));
        await Assert.ThrowsAsync<ExamAttemptNotFoundException>(() =>
            sender.Send(new GetExamAttemptMediaUrlQuery(UserId, sheet.AttemptId, ExamMediaKind.Audio,
                Guid.NewGuid())));
    }

    private sealed class RecordingExamMediaStore : IExamMediaStore
    {
        public int BatchCallCount { get; private set; }
        public int SingleGetCallCount { get; private set; }
        public TimeSpan? LastBatchLifetime { get; private set; }
        public IReadOnlyList<string> LastBatchKeys { get; private set; } = [];

        public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            SingleGetCallCount++;
            return Task.FromResult($"https://minio.local/single/{objectKey}");
        }

        public Task<IReadOnlyList<string>> CreatePresignedGetUrlsAsync(IReadOnlyList<string> objectKeys,
            TimeSpan lifetime, CancellationToken cancellationToken)
        {
            BatchCallCount++;
            LastBatchLifetime = lifetime;
            LastBatchKeys = objectKeys.ToArray();
            IReadOnlyList<string> urls = objectKeys
                .Select((_, index) => $"https://minio.local/signed/{index}")
                .ToArray();
            return Task.FromResult(urls);
        }
    }
}
