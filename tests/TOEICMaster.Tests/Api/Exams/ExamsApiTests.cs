using System.Net;
using System.Net.Http.Headers;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IExamRepository>(new FakeExamRepository(ScoreProfile));
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
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

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
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.GetAsync("/api/v1/admin/score-profiles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.ScoreProfile.Id.ToString(), result[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Admin_creates_a_score_profile_with_string_skill_values()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

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
    public async Task Import_rejects_an_invalid_workbook_without_creating_a_draft()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("not an XLSX")), "workbook", "exam.xlsx");
        var audio = new ByteArrayContent("RIFF"u8.ToArray().Concat(new byte[4]).Concat("WAVEfmt "u8.ToArray()).ToArray());
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "masterAudio", "listening.wav");

        using var response = await client.PostAsync("/api/v1/admin/exams/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("errors").TryGetProperty("workbook", out _));
    }
}

internal sealed class FakeExamRepository(ScoreProfile scoreProfile) : IExamRepository
{
    private readonly List<Exam> _exams = [];

    public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_exams.Any(exam => string.Equals(exam.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult<Exam?>(_exams.SingleOrDefault(exam => exam.Id == examId));

    public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
        CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));

    public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
        Task.FromResult<ScoreProfile?>(scoreProfile.Id == scoreProfileId ? scoreProfile : null);

    public Task<IReadOnlyList<ScoreProfileDto>> GetScoreProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScoreProfileDto>>([ScoreProfileDto.From(scoreProfile)]);

    public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
    {
        _exams.Add(exam);
        return Task.CompletedTask;
    }

    public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
