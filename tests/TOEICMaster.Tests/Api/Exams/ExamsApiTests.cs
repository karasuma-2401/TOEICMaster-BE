using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Exams;

public sealed class ExamsApiFactory : ApiFactory
{
    public ScoreProfile ScoreProfile { get; } = ScoreProfile.Create("Reference profile", DateTimeOffset.UtcNow);
    internal FakeExamRepository Repository { get; }
    internal FakeExamMediaStore MediaStore { get; } = new();

    public ExamsApiFactory() => Repository = new FakeExamRepository(ScoreProfile);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IExamRepository>(Repository);
            services.AddSingleton<IExamMediaStore>(MediaStore);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, ExamTestAuthenticationHandler>("Test", _ => { });
        });
    }
}

public sealed class ExamTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class ExamsApiTests(ExamsApiFactory factory) : IClassFixture<ExamsApiFactory>
{
    private HttpClient CreateAdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private async Task<(Guid ExamId, Guid RevisionId)> CreateDraftAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/admin/exams", new
        {
            code,
            title = "Practice test",
            scoreProfileId = factory.ScoreProfile.Id
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (result.GetProperty("examId").GetGuid(), result.GetProperty("revisionId").GetGuid());
    }

    [Fact]
    public async Task Published_revision_can_be_forked()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, $"LR-FORK-{Guid.NewGuid():N}");
        var exam = (await factory.Repository.FindExamAsync(examId, CancellationToken.None))!;
        var published = exam.Revisions.Single();
        published.ReplaceContextBlocks("P3-G32", [new ExamContextContent(1, ExamContextType.Text, "Passage", null, null)]);
        exam.PublishDraft(revisionId);

        using var response = await client.PostAsJsonAsync($"/api/v1/admin/exams/{examId}/revisions",
            new { sourceRevisionId = revisionId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(revisionId, body.GetProperty("revisionId").GetGuid());
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal(2, body.GetProperty("versionNumber").GetInt32());
        Assert.Equal(ExamRevisionStatus.Published, published.Status);

    }

    [Fact]
    public async Task Draft_revision_cannot_be_forked()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, $"LR-FORK-{Guid.NewGuid():N}");
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/exams/{examId}/revisions",
            new { sourceRevisionId = revisionId });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Published_revision_cannot_be_published_again()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, $"LR-PUBLISH-{Guid.NewGuid():N}");
        var exam = (await factory.Repository.FindExamAsync(examId, CancellationToken.None))!;
        exam.PublishDraft(revisionId);
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/publish", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(ExamRevisionStatus.Published)]
    [InlineData(ExamRevisionStatus.Superseded)]
    public async Task Non_draft_revision_cannot_be_edited(ExamRevisionStatus status)
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, $"LR-EDIT-{Guid.NewGuid():N}");
        var exam = (await factory.Repository.FindExamAsync(examId, CancellationToken.None))!;
        exam.PublishDraft(revisionId);
        if (status == ExamRevisionStatus.Superseded)
        {
            var next = exam.CreateDraftRevision(revisionId, DateTimeOffset.UtcNow);
            exam.PublishDraft(next.Id);
        }
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/questions/1",
            new { code = "Q1", stem = "Stem", optionA = "A", optionB = "B", optionC = "C", optionD = "D", correctKey = "A" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    public async Task Creating_an_exam_requires_an_authenticated_admin(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);

        using var response = await client.PostAsJsonAsync("/api/v1/admin/exams", new
        {
            code = "LR-2026-001",
            title = "Practice test 1",
            scoreProfileId = factory.ScoreProfile.Id
        });

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_a_draft_with_200_slots()
    {
        using var client = CreateAdminClient();

        using var response = await client.PostAsJsonAsync("/api/v1/admin/exams", new
        {
            code = "LR-2026-002",
            title = "Practice test 2",
            scoreProfileId = factory.ScoreProfile.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LR-2026-002", result.GetProperty("code").GetString());
        Assert.Equal("Draft", result.GetProperty("status").GetString());
        Assert.Equal(200, result.GetProperty("questionCount").GetInt32());
    }

    [Fact]
    public async Task Admin_lists_reusable_score_profiles()
    {
        using var client = CreateAdminClient();

        using var response = await client.GetAsync("/api/v1/admin/score-profiles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.ScoreProfile.Id.ToString(), result[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Admin_creates_a_score_profile_with_string_skill_values()
    {
        using var client = CreateAdminClient();

        using var response = await client.PostAsJsonAsync("/api/v1/admin/score-profiles", new
        {
            name = "Partial reference profile",
            conversions = new[] { new { skill = "Listening", rawCorrectCount = 0, scaledScore = 5 } }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Partial reference profile", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Admin_creates_a_score_profile_accepting_integer_enum_values()
    {
        using var client = CreateAdminClient();

        using var response = await client.PostAsJsonAsync("/api/v1/admin/score-profiles", new
        {
            name = "Integer enum profile",
            conversions = new[] { new { skill = 0, rawCorrectCount = 0, scaledScore = 5 } }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Integer enum profile", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Import_rejects_an_invalid_workbook_without_creating_a_draft()
    {
        using var client = CreateAdminClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("not an XLSX")), "workbook", "exam.xlsx");

        using var response = await client.PostAsync("/api/v1/admin/exams/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("errors").TryGetProperty("workbook", out _));
    }

    [Fact]
    public async Task Audio_upload_intent_persists_duration_and_revision_detail_returns_audio_duration_without_legacy_flags()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, "LR-2026-DUR-1");
        factory.MediaStore.NextMetadata = new ExamMediaObjectMetadata(1024, "audio/mpeg", "etag-1");

        using var intentResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents",
            new
            {
                ownerKind = "AudioGroup",
                ownerKey = "P1-Q1",
                contentType = "audio/mp3",
                byteSize = 1024,
                durationMs = 31_250
            });
        Assert.Equal(HttpStatusCode.Created, intentResponse.StatusCode);
        var intentJson = await intentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var intentId = intentJson.GetProperty("intentId").GetGuid();
        Assert.Equal("audio/mpeg", intentJson.GetProperty("requiredHeaders").GetProperty("Content-Type").GetString());

        using var completeResponse = await client.PostAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents/{intentId}/complete",
            content: null);
        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);
        var assetJson = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(31_250, assetJson.GetProperty("durationMs").GetInt32());

        using var detailResponse = await client.GetAsync($"/api/v1/admin/exams/{examId}/revisions/{revisionId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailJson = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(detailJson.TryGetProperty("hasMasterAudio", out _));
        Assert.False(detailJson.TryGetProperty("usesMediaWorkspace", out _));
        var p1Block = detailJson.GetProperty("blocks").EnumerateArray()
            .Single(block => block.GetProperty("key").GetString() == "P1-Q1");
        Assert.Equal(31_250, p1Block.GetProperty("audioDurationMs").GetInt32());
    }

    [Fact]
    public async Task Question_image_upload_intent_is_allowed_for_part_1()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, "LR-2026-IMG-P1");

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents",
            new
            {
                ownerKind = "QuestionImage",
                ownerKey = "1",
                contentType = "image/png",
                byteSize = 2048
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("AudioGroup", "P1-Q1", "audio/mpeg", 0)]
    [InlineData("AudioGroup", "P1-Q1", "audio/mpeg", 3_600_001)]
    [InlineData("QuestionImage", "1", "image/png", 5_000)]
    public async Task Media_upload_intent_rejects_invalid_duration_with_validation_problem_details(
        string ownerKind, string ownerKey, string contentType, int durationMs)
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, $"LR-2026-VAL-{Guid.NewGuid():N}"[..16]);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents",
            new
            {
                ownerKind,
                ownerKey,
                contentType,
                byteSize = 1024,
                durationMs
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Legacy_master_audio_upload_endpoint_is_removed()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, "LR-2026-LEG-1");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3, 4]), "file", "master.mp3");

        using var response = await client.PutAsync(
            $"/api/v1/admin/exams/{examId}/revisions/{revisionId}/master-audio", form);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Admin_exam_list_and_revision_detail_serialize_enums_as_PascalCase_strings()
    {
        using var client = CreateAdminClient();
        var (examId, revisionId) = await CreateDraftAsync(client, "LR-2026-ENUM-1");

        // Add both Text and Image context blocks to verify ExamContextType serialization
        var targetExam = await factory.Repository.FindExamAsync(examId, CancellationToken.None);
        var revision = targetExam!.Revisions.Single(r => r.Id == revisionId);
        revision.ReplaceContextBlocks("P3-G32",
        [
            new ExamContextContent(1, ExamContextType.Text, "Read the passage.", null, null, false),
            new ExamContextContent(2, ExamContextType.Image, null, Guid.NewGuid(), "Chart", false)
        ]);

        using var listResponse = await client.GetAsync("/api/v1/admin/exams?pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listItem = listJson.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("examId").GetGuid() == examId);
        Assert.Equal("Draft", listItem.GetProperty("status").GetString());

        using var detailResponse = await client.GetAsync($"/api/v1/admin/exams/{examId}/revisions/{revisionId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailJson = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", detailJson.GetProperty("status").GetString());

        var blocks = detailJson.GetProperty("blocks").EnumerateArray().ToArray();
        Assert.Contains(blocks, b => b.GetProperty("type").GetString() == "Single");
        var groupBlock = blocks.First(b => b.GetProperty("key").GetString() == "P3-G32");
        Assert.Equal("Group", groupBlock.GetProperty("type").GetString());

        var contextTypes = groupBlock.GetProperty("contextBlocks").EnumerateArray()
            .Select(c => c.GetProperty("type").GetString()).ToArray();
        Assert.Equal(new[] { "Text", "Image" }, contextTypes);

        targetExam.PublishDraft(revisionId);
        using var publishedListResponse = await client.GetAsync("/api/v1/admin/exams?pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, publishedListResponse.StatusCode);
        var publishedListItem = (await publishedListResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("examId").GetGuid() == examId);
        Assert.Equal("Published", publishedListItem.GetProperty("status").GetString());

        using var publishedDetailResponse = await client.GetAsync($"/api/v1/admin/exams/{examId}/revisions/{revisionId}");
        Assert.Equal(HttpStatusCode.OK, publishedDetailResponse.StatusCode);
        Assert.Equal("Published",
            (await publishedDetailResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}

internal sealed class FakeExamMediaStore : IExamMediaStore
{
    public ExamMediaObjectMetadata? NextMetadata { get; set; }

    public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan lifetime,
        CancellationToken cancellationToken) => Task.FromResult($"https://minio.local/put/{objectKey}");

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan lifetime,
        CancellationToken cancellationToken) => Task.FromResult($"https://minio.local/get/{objectKey}");

    public Task<IReadOnlyList<string>> CreatePresignedGetUrlsAsync(IReadOnlyList<string> objectKeys, TimeSpan lifetime,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(objectKeys.Select(key => $"https://minio.local/get/{key}").ToArray());

    public Task<ExamMediaObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(NextMetadata);
}

internal sealed class FakeExamRepository(ScoreProfile scoreProfile) : IExamRepository
{
    private readonly List<Exam> _exams = [];
    private readonly Dictionary<Guid, ExamMediaUploadIntent> _intents = [];
    private readonly Dictionary<Guid, ExamMediaAsset> _assets = [];

    public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_exams.Any(exam => string.Equals(exam.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult<Exam?>(_exams.SingleOrDefault(exam => exam.Id == examId));

    public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        var items = _exams
            .Select(exam =>
            {
                var latest = exam.Revisions.OrderByDescending(r => r.VersionNumber).First();
                return new ExamListItemDto(exam.Id, exam.Code, exam.Title, latest.Id, latest.VersionNumber,
                    latest.Status, exam.CreatedAtUtc);
            })
            .Where(item => status is null || item.Status == status)
            .ToArray();
        return Task.FromResult(new ExamPageDto(
            items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            items.Length));
    }

    public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
        Task.FromResult<ScoreProfile?>(scoreProfile.Id == scoreProfileId ? scoreProfile : null);

    public Task<IReadOnlyList<ScoreProfileDto>> GetScoreProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScoreProfileDto>>([ScoreProfileDto.From(scoreProfile)]);

    public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyDictionary<Guid, int?>> GetMediaDurationsAsync(IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, int?> durations = assetIds
            .Where(_assets.ContainsKey)
            .ToDictionary(id => id, id => _assets[id].DurationMs);
        return Task.FromResult(durations);
    }

    public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
    {
        _exams.Add(exam);
        return Task.CompletedTask;
    }

    public Task AddScoreProfileAsync(ScoreProfile profile, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken)
    {
        _assets[asset.Id] = asset;
        return Task.CompletedTask;
    }

    public Task AddExamMediaUploadIntentAsync(ExamMediaUploadIntent intent, CancellationToken cancellationToken)
    {
        _intents[intent.Id] = intent;
        return Task.CompletedTask;
    }

    public Task<ExamMediaUploadIntent?> FindExamMediaUploadIntentAsync(Guid intentId, CancellationToken cancellationToken) =>
        Task.FromResult(_intents.GetValueOrDefault(intentId));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
