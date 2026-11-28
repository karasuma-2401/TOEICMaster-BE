using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.ExerciseBank;

public sealed class LrPracticeApiFactory : ApiFactory
{
    public LrExerciseBankItem Item { get; } = CreateItem();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:PublicEndpoint"] = "http://127.0.0.1:9000"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILrExerciseBankRepository>(new InMemoryLrPracticeExerciseBankRepository(Item));
            services.AddSingleton<ILrPracticeAttemptRepository>(new InMemoryLrPracticeAttemptRepository());
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "LrPracticeTest";
                    options.DefaultChallengeScheme = "LrPracticeTest";
                    options.DefaultForbidScheme = "LrPracticeTest";
                })
                .AddScheme<AuthenticationSchemeOptions, LrPracticeTestAuthenticationHandler>(
                    "LrPracticeTest", _ => { });
        });
    }

    private static LrExerciseBankItem CreateItem() => LrExerciseBankItem.Create(
        LrExercisePart.Part1,
        "P1-API-001",
        "API test",
        LrExerciseDifficulty.Easy,
        LrExerciseStatus.Published,
        null,
        "https://example.test/audio.mp3",
        "https://example.test/image.png",
        [new LrExerciseBankQuestionInput(
            "P1-API-001-Q1",
            "What is shown?",
            "A report",
            "A flight",
            "A meeting",
            "A lunch",
            "A",
            "Translation",
            "Explanation")],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}

public sealed class LrPracticeTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class LrPracticeApiTests : IClassFixture<LrPracticeApiFactory>
{
    private readonly LrPracticeApiFactory _factory;

    public LrPracticeApiTests(LrPracticeApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Listing_rejects_invalid_part_with_problem_details(int part)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/lr-practice/parts/{part}/items");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Listing_rejects_page_number_that_overflows_the_sql_offset()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/v1/lr-practice/parts/1/items?pageNumber=2147483647&pageSize=50");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Detail_does_not_leak_answer_fields()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/lr-practice/items/{_factory.Item.Id}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("questions")[0].TryGetProperty("correctKey", out _));
        Assert.False(body.GetProperty("questions")[0].TryGetProperty("translation", out _));
        Assert.False(body.GetProperty("questions")[0].TryGetProperty("explanation", out _));
    }

    [Theory]
    [InlineData("/api/v1/lr-practice/items/00000000-0000-0000-0000-000000000001/submit")]
    [InlineData("/api/v1/lr-practice/parts/1/statistics")]
    public async Task Protected_endpoints_require_authentication(string path)
    {
        using var client = _factory.CreateClient();
        using var response = path.EndsWith("/submit", StringComparison.Ordinal)
            ? await client.PostAsJsonAsync(path, new { answers = Array.Empty<object>() })
            : await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_returns_problem_details_for_invalid_option_key()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = _factory.Item.Questions.Single().Id, selectedKey = "E" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Detail_returns_not_found_for_unknown_item()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/lr-practice/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_not_found_for_unknown_item()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{Guid.NewGuid()}/submit",
            new { answers = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_problem_details_for_null_answers()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new { answers = (object?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_problem_details_for_duplicate_question_id()
    {
        var questionId = _factory.Item.Questions.Single().Id;
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId, selectedKey = "A" },
                    new { questionId, selectedKey = "B" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_problem_details_for_foreign_question_id()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = Guid.NewGuid(), selectedKey = "A" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_problem_details_for_overlong_selected_key()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = _factory.Item.Questions.Single().Id, selectedKey = "ABCDEFGHI" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Submit_returns_graded_result_for_valid_answers()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{_factory.Item.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = _factory.Item.Questions.Single().Id, selectedKey = " a " }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LrPracticeSubmitResultDto>();
        Assert.NotNull(body);
        Assert.Equal(_factory.Item.Id, body.ItemId);
        Assert.Equal(1, body.TotalQuestions);
        Assert.Equal(1, body.CorrectCount);
        Assert.Equal(0, body.IncorrectCount);
        var question = Assert.Single(body.Questions);
        Assert.Equal("A", question.SelectedKey);
        Assert.Equal("A", question.CorrectKey);
        Assert.True(question.IsCorrect);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Statistics_rejects_invalid_part_with_problem_details(int part)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.GetAsync($"/api/v1/lr-practice/parts/{part}/statistics");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

}

internal sealed class InMemoryLrPracticeExerciseBankRepository(LrExerciseBankItem item) : ILrExerciseBankRepository
{
    public Task<LrExerciseBankItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(itemId == item.Id ? item : null);
    public Task<LrExerciseItemPageDto> GetPageAsync(LrExerciseItemFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(new LrExerciseItemPageDto([], filter.PageNumber, filter.PageSize, 0));

    public Task<(IReadOnlyList<LrExerciseBankItem> Items, int TotalCount)> GetPublishedPageAsync(
        int part,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromResult(((IReadOnlyList<LrExerciseBankItem>)[item], 1));

    public Task<LrExerciseBankItem?> GetPublishedByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(itemId == item.Id ? item : null);

    public Task<LrExerciseBankSummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new LrExerciseBankSummaryDto(0, null, []));

    public Task<bool> ItemCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task AddItemsAsync(IReadOnlyCollection<LrExerciseBankItem> items, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class InMemoryLrPracticeAttemptRepository : ILrPracticeAttemptRepository
{
    public Task AddAsync(LrPracticeAttempt attempt, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<LrPracticeAttempt>> GetByUserAndItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LrPracticeAttempt>>([]);

    public Task<IReadOnlyList<LrPracticeItemStatisticsDto>> GetStatisticsByUserAndPartAsync(
        Guid userId,
        int part,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LrPracticeItemStatisticsDto>>([]);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
