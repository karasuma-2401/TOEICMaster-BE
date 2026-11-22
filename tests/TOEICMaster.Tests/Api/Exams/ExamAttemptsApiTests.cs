using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Application.Exams;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Exams;

public sealed class ExamAttemptsApiFactory : ApiFactory
{
    public InMemoryExamAttempts Attempts { get; } = new();
    internal RecordingAttemptMediaStore MediaStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IExamAttemptRepository>(Attempts);
            services.AddSingleton<IExamMediaStore>(MediaStore);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "AttemptTest";
                    options.DefaultChallengeScheme = "AttemptTest";
                    options.DefaultForbidScheme = "AttemptTest";
                })
                .AddScheme<AuthenticationSchemeOptions, AttemptTestAuthenticationHandler>("AttemptTest", _ => { });
        });
    }
}

public sealed class AttemptTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

internal sealed class RecordingAttemptMediaStore : IExamMediaStore
{
    public int BatchCallCount { get; private set; }

    public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan lifetime,
        CancellationToken cancellationToken) => Task.FromResult($"https://minio.local/single/{objectKey}");

    public Task<IReadOnlyList<string>> CreatePresignedGetUrlsAsync(IReadOnlyList<string> objectKeys, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        BatchCallCount++;
        return Task.FromResult<IReadOnlyList<string>>(
            objectKeys.Select(key => $"https://minio.local/batch/{key}").ToArray());
    }
}

public sealed class ExamAttemptsApiTests : IDisposable
{
    private readonly ExamAttemptsApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateUserClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        return client;
    }

    private async Task<Guid> StartAttemptAsync(HttpClient client)
    {
        using var response = await client.PostAsync($"/api/v1/exams/{_factory.Attempts.Exam.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertSheetHasAudioDurationAndNoMasterAudioKey(json);
        return json.GetProperty("attemptId").GetGuid();
    }

    private static void AssertSheetHasAudioDurationAndNoMasterAudioKey(JsonElement sheet)
    {
        Assert.False(sheet.TryGetProperty("masterAudioObjectKey", out _));
        var p1Block = sheet.GetProperty("blocks").EnumerateArray()
            .Single(block => block.GetProperty("firstQuestionNumber").GetInt32() == 1);
        Assert.Equal(18_500, p1Block.GetProperty("audioDurationMs").GetInt32());
    }

    [Fact]
    public async Task Published_lr_catalog_allows_anonymous_and_authenticated_requests()
    {
        using var guestClient = _factory.CreateClient();
        using var guestResponse = await guestClient.GetAsync("/api/v1/exams/lr");
        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);

        using var userClient = CreateUserClient(Guid.NewGuid());
        using var userResponse = await userClient.GetAsync("/api/v1/exams/lr");
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
    }

    [Fact]
    public async Task Listening_audio_urls_requires_authentication()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/exam-attempts/{Guid.NewGuid()}/listening-audio-urls");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sheet_endpoints_include_audio_duration_and_omit_master_audio_key()
    {
        var userId = Guid.NewGuid();
        using var client = CreateUserClient(userId);
        var attemptId = await StartAttemptAsync(client);

        using var currentResponse = await client.GetAsync($"/api/v1/exams/{_factory.Attempts.Exam.Id}/current-attempt");
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        AssertSheetHasAudioDurationAndNoMasterAudioKey(await currentResponse.Content.ReadFromJsonAsync<JsonElement>());

        using var progressResponse = await client.GetAsync($"/api/v1/exam-attempts/{attemptId}");
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        AssertSheetHasAudioDurationAndNoMasterAudioKey(await progressResponse.Content.ReadFromJsonAsync<JsonElement>());

        using var readingResponse = await client.PostAsync($"/api/v1/exam-attempts/{attemptId}/start-reading", null);
        Assert.Equal(HttpStatusCode.OK, readingResponse.StatusCode);
        AssertSheetHasAudioDurationAndNoMasterAudioKey(await readingResponse.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task Listening_audio_urls_returns_batch_signed_items_and_enforces_ownership_and_phase_lock()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        using var ownerClient = CreateUserClient(ownerId);
        using var otherClient = CreateUserClient(otherId);
        var attemptId = await StartAttemptAsync(ownerClient);

        using var otherResponse = await otherClient.GetAsync($"/api/v1/exam-attempts/{attemptId}/listening-audio-urls");
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);

        using var playlistResponse = await ownerClient.GetAsync($"/api/v1/exam-attempts/{attemptId}/listening-audio-urls");
        Assert.Equal(HttpStatusCode.OK, playlistResponse.StatusCode);
        var playlist = await playlistResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = playlist.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(1, items[0].GetProperty("part").GetInt32());
        Assert.Equal(1, items[0].GetProperty("firstQuestionNumber").GetInt32());
        Assert.Equal(_factory.Attempts.PartOneAudioAsset.Id, items[0].GetProperty("audioAssetId").GetGuid());
        Assert.Equal(18_500, items[0].GetProperty("durationMs").GetInt32());
        Assert.Equal("https://minio.local/batch/exam-media/p1-q1.mp3", items[0].GetProperty("url").GetString());
        Assert.True(items[0].TryGetProperty("expiresAtUtc", out _));
        Assert.Equal(1, _factory.MediaStore.BatchCallCount);

        using var startReading = await ownerClient.PostAsync($"/api/v1/exam-attempts/{attemptId}/start-reading", null);
        Assert.Equal(HttpStatusCode.OK, startReading.StatusCode);

        using var lockedPlaylist = await ownerClient.GetAsync($"/api/v1/exam-attempts/{attemptId}/listening-audio-urls");
        Assert.Equal(HttpStatusCode.Conflict, lockedPlaylist.StatusCode);

        using var lockedAudio = await ownerClient.GetAsync(
            $"/api/v1/exam-attempts/{attemptId}/audio/{_factory.Attempts.PartOneAudioAsset.Id}");
        Assert.Equal(HttpStatusCode.Conflict, lockedAudio.StatusCode);

        using var allowedImage = await ownerClient.GetAsync(
            $"/api/v1/exam-attempts/{attemptId}/images/{_factory.Attempts.PartOneImageAsset.Id}");
        Assert.Equal(HttpStatusCode.OK, allowedImage.StatusCode);
    }

    [Fact]
    public async Task Completed_attempt_allows_review_audio_and_block_audio_metadata_but_rejects_listening_playlist()
    {
        var userId = Guid.NewGuid();
        using var client = CreateUserClient(userId);
        var attemptId = await StartAttemptAsync(client);

        using var submitResponse = await client.PostAsync($"/api/v1/exam-attempts/{attemptId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using var playlistResponse = await client.GetAsync($"/api/v1/exam-attempts/{attemptId}/listening-audio-urls");
        Assert.Equal(HttpStatusCode.Conflict, playlistResponse.StatusCode);

        using var audioResponse = await client.GetAsync(
            $"/api/v1/exam-attempts/{attemptId}/audio/{_factory.Attempts.PartOneAudioAsset.Id}");
        Assert.Equal(HttpStatusCode.OK, audioResponse.StatusCode);

        using var reviewResponse = await client.GetAsync($"/api/v1/exam-attempts/{attemptId}/review?filter=All");
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = await reviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var blocks = review.GetProperty("blocks").EnumerateArray().ToArray();
        var p1Block = blocks.Single(b => b.GetProperty("firstQuestionNumber").GetInt32() == 1);
        var p5Block = blocks.Single(b => b.GetProperty("firstQuestionNumber").GetInt32() == 101);
        Assert.Equal(_factory.Attempts.PartOneAudioAsset.Id, p1Block.GetProperty("audioAssetId").GetGuid());
        Assert.Equal(18_500, p1Block.GetProperty("audioDurationMs").GetInt32());
        Assert.Equal(JsonValueKind.Null, p5Block.GetProperty("audioAssetId").ValueKind);
        Assert.Equal(JsonValueKind.Null, p5Block.GetProperty("audioDurationMs").ValueKind);
    }

    [Fact]
    public async Task Legacy_master_audio_attempt_endpoint_is_removed()
    {
        var userId = Guid.NewGuid();
        using var client = CreateUserClient(userId);
        var attemptId = await StartAttemptAsync(client);

        using var response = await client.GetAsync($"/api/v1/exam-attempts/{attemptId}/audio");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Attempt_history_serializes_status_enum_as_PascalCase_strings()
    {
        var userId = Guid.NewGuid();
        using var client = CreateUserClient(userId);
        var attemptId = await StartAttemptAsync(client);

        using var inProgressHistory = await client.GetAsync("/api/v1/exam-attempts/history");
        Assert.Equal(HttpStatusCode.OK, inProgressHistory.StatusCode);
        var inProgressJson = await inProgressHistory.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", inProgressJson.GetProperty("items")[0].GetProperty("status").GetString());

        using var submitResponse = await client.PostAsync($"/api/v1/exam-attempts/{attemptId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using var completedHistory = await client.GetAsync("/api/v1/exam-attempts/history");
        Assert.Equal(HttpStatusCode.OK, completedHistory.StatusCode);
        var completedJson = await completedHistory.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", completedJson.GetProperty("items")[0].GetProperty("status").GetString());
    }
}
