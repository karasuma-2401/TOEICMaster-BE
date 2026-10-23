using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Speaking;

public sealed class SpeakingApiFactory : ApiFactory
{
    public InMemorySpeaking Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISpeakingAdminRepository>(Store);
            services.AddSingleton<ISpeakingAttemptRepository>(Store);
            services.AddSingleton<ISpeakingMediaStore>(Store);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "SpeakingTest";
                    options.DefaultChallengeScheme = "SpeakingTest";
                    options.DefaultForbidScheme = "SpeakingTest";
                })
                .AddScheme<AuthenticationSchemeOptions, SpeakingTestAuthenticationHandler>("SpeakingTest", _ => { });
        });
    }
}

// Test host only: X-Test-User becomes the JWT subject and X-Test-Role its role.
public sealed class SpeakingTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var user))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.ToString()) };
        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}

public sealed class SpeakingApiTests : IDisposable
{
    private const string Admin = "/api/v1/admin/speaking";
    private const string Learner = "/api/v1/speaking";
    private readonly SpeakingApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Administration_requires_an_admin(string? role, HttpStatusCode expected)
    {
        using var client = _factory.CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        using var response = await client.GetAsync($"{Admin}/summary");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Admin_and_learner_contract_matches_the_frontend()
    {
        using var admin = AdminClient();

        // Image upload and validation errors keyed by field.
        using var image = await UploadAsync(admin, $"{Admin}/media/images", SpeakingSamples.Png, "image/png");
        Assert.Equal(HttpStatusCode.Created, image.StatusCode);
        var imageJson = await JsonAsync(image);
        Assert.Equal(("image/png", SpeakingSamples.Png.Length),
            (imageJson["contentType"]!.GetValue<string>(), imageJson["sizeBytes"]!.GetValue<int>()));
        var imageId = Guid.Parse(imageJson["id"]!.GetValue<string>());

        using var invalid = await admin.PostAsJsonAsync($"{Admin}/exercises",
            SpeakingSamples.Exercise(1, "SP1-BAD") with { Passage = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.NotNull((await JsonAsync(invalid))["errors"]!["Passage"]);

        // Exercises: content fields are flat and the image is referenced by id only.
        var slots = new List<SpeakingTestSlotInput>();
        foreach (var part in Enumerable.Range(1, 5))
            for (var sequence = 1; sequence <= TOEICMaster.Domain.Entities.SpeakingTiming.SlotsPerPart(part); sequence++)
            {
                using var created = await admin.PostAsJsonAsync($"{Admin}/exercises",
                    SpeakingSamples.Exercise(part, $"SP{part}-00{sequence}", imageId));
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                var exercise = await JsonAsync(created);
                Assert.Equal(part, exercise["part"]!.GetValue<int>());
                Assert.Null(exercise["content"]);
                Assert.NotNull(exercise["updatedAtUtc"]);
                if (part == 2)
                    Assert.Equal(imageId.ToString(), exercise["imageAssetId"]!.GetValue<string>());
                if (part == 4)
                    Assert.Equal("Registration", exercise["documentRows"]![0]!["value"]!.GetValue<string>());
                slots.Add(new SpeakingTestSlotInput(Guid.Parse(exercise["id"]!.GetValue<string>()), part, sequence));
            }
        using var duplicate = await admin.PostAsJsonAsync($"{Admin}/exercises", SpeakingSamples.Exercise(5, "sp5-001"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // Tests: draft, publish requirements (422), publish, list.
        using var incomplete = await admin.PostAsJsonAsync($"{Admin}/tests",
            new SpeakingTestInput("SPK-DRAFT", "Draft", "", slots.Take(1).ToArray()));
        var draft = await JsonAsync(incomplete);
        using var notReady = await admin.PostAsJsonAsync(
            $"{Admin}/tests/{draft["id"]}/publish", new { version = draft["version"]!.GetValue<int>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notReady.StatusCode);
        Assert.NotNull((await JsonAsync(notReady))["errors"]!["Part5"]);

        using var createdTest = await admin.PostAsJsonAsync($"{Admin}/tests",
            new SpeakingTestInput("SPK-2026-01", "TOEIC Speaking Practice Test 01", "Đề đủ 11 câu.", slots));
        Assert.Equal(HttpStatusCode.Created, createdTest.StatusCode);
        var test = await JsonAsync(createdTest);
        Assert.Equal(("DRAFT", 7), (test["status"]!.GetValue<string>(), test["exercises"]!.AsArray().Count));
        Assert.Equal("SP1-001", test["exercises"]![0]!["exercise"]!["code"]!.GetValue<string>());
        var testId = test["id"]!.GetValue<string>();
        using var stale = await admin.PostAsJsonAsync($"{Admin}/tests/{testId}/publish", new { version = 99 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var published = await admin.PostAsJsonAsync($"{Admin}/tests/{testId}/publish",
            new { version = test["version"]!.GetValue<int>() });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.NotNull((await JsonAsync(published))["publishedAtUtc"]);

        using var list = await admin.GetAsync($"{Admin}/tests?status=PUBLISHED&pageNumber=1&pageSize=10");
        var listed = (await JsonAsync(list))["items"]![0]!;
        Assert.Equal(11, listed["questionCount"]!.GetValue<int>());
        Assert.Equal([2, 2, 1, 1, 1], Enumerable.Range(1, 5).Select(part =>
            listed["partCounts"]![part.ToString()]!.GetValue<int>()));
        using var bank = await admin.GetAsync($"{Admin}/exercises?part=4&query=sp4&pageNumber=1&pageSize=100");
        var bankJson = await JsonAsync(bank);
        Assert.Equal((1, 1), (bankJson["totalCount"]!.GetValue<int>(), bankJson["totalPages"]!.GetValue<int>()));
        Assert.True(list.Headers.CacheControl?.NoStore);

        // Learner: published test summary without review material.
        using var guest = GuestClient();
        using var tests = await guest.GetAsync($"{Learner}/tests?pageNumber=1&pageSize=50");
        var publicJson = (await tests.Content.ReadAsStringAsync());
        Assert.DoesNotContain("rubricNotes", publicJson);
        Assert.DoesNotContain("modelAnswer", publicJson);
        var parts = JsonNode.Parse(publicJson)!["items"]![0]!["parts"]!.AsArray();
        Assert.Equal([180, 150, 69, 114, 105], parts.Select(part => part!["durationSeconds"]!.GetValue<int>()));
        using var picture = await guest.GetAsync($"{Learner}/media/{imageId}");
        Assert.Equal(("image/png", true, true), (picture.Content.Headers.ContentType?.MediaType,
            picture.Headers.CacheControl?.Private, picture.Headers.CacheControl?.NoStore));

        // Attempts need an owner.
        using var anonymous = _factory.CreateClient();
        using var noOwner = await anonymous.PostAsJsonAsync($"{Learner}/attempts", new { testId, mode = "FULL" });
        Assert.Equal(HttpStatusCode.Unauthorized, noOwner.StatusCode);

        using var started = await guest.PostAsJsonAsync($"{Learner}/attempts", new { testId, mode = "PART_4" });
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var attempt = await JsonAsync(started);
        var attemptId = attempt["id"]!.GetValue<string>();
        Assert.Equal(("PART_4", "IN_PROGRESS", "READING", 1), (attempt["mode"]!.GetValue<string>(),
            attempt["status"]!.GetValue<string>(), attempt["currentPhase"]!.GetValue<string>(),
            attempt["currentQuestionPosition"]!.GetValue<int>()));
        Assert.NotNull(attempt["serverNowUtc"]);
        var first = attempt["questions"]![0]!;
        Assert.Equal((4, 0, 45, 3, 15), (first["part"]!.GetValue<int>(), first["questionIndex"]!.GetValue<int>(),
            first["readingSeconds"]!.GetValue<int>(), first["preparationSeconds"]!.GetValue<int>(),
            first["responseSeconds"]!.GetValue<int>()));
        Assert.Equal(4, first["content"]!["part"]!.GetValue<int>());
        Assert.All(first["content"]!["questions"]!.AsArray(), Assert.Null);
        Assert.Null(first["recording"]);
        Assert.Empty(attempt["reviewQuestions"]!.AsArray());

        using var stranger = GuestClient();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Learner}/attempts/{attemptId}")).StatusCode);

        // Upload only while responding; then replay the recording.
        using var early = await UploadAsync(guest, $"{Learner}/attempts/{attemptId}/recordings/1",
            SpeakingSamples.WebmAudio, "audio/webm;codecs=opus", 12);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        await guest.PostAsJsonAsync($"{Learner}/attempts/{attemptId}/next", new { });
        using var responding = await guest.PostAsJsonAsync($"{Learner}/attempts/{attemptId}/next", new { });
        Assert.Equal("RESPONDING", (await JsonAsync(responding))["currentPhase"]!.GetValue<string>());
        using var fake = await UploadAsync(guest, $"{Learner}/attempts/{attemptId}/recordings/1",
            SpeakingSamples.Png, "audio/webm", 12);
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        using var recorded = await UploadAsync(guest, $"{Learner}/attempts/{attemptId}/recordings/1",
            SpeakingSamples.WebmAudio, "audio/webm;codecs=opus", 12);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(12, (await JsonAsync(recorded))["questions"]![0]!["recording"]!["durationSeconds"]!.GetValue<int>());
        using var audio = await guest.GetAsync($"{Learner}/attempts/{attemptId}/recordings/1");
        Assert.Equal("audio/webm", audio.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SpeakingSamples.WebmAudio, await audio.Content.ReadAsByteArrayAsync());

        // Submitting reveals every question and the review material.
        using var submitted = await guest.PostAsJsonAsync($"{Learner}/attempts/{attemptId}/submit", new { });
        var done = await JsonAsync(submitted);
        Assert.Equal("SUBMITTED", done["status"]!.GetValue<string>());
        Assert.Null(done["currentPhase"]);
        Assert.Equal("Sessions after lunch?", done["questions"]![2]!["content"]!["questions"]![2]!.GetValue<string>());
        Assert.Equal("Model answer SP4-001.", done["reviewQuestions"]![0]!["modelAnswer"]!.GetValue<string>());
        using var history = await guest.GetAsync($"{Learner}/attempts");
        Assert.Equal(attemptId, (await JsonAsync(history))["items"]![0]!["id"]!.GetValue<string>());
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient GuestClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Speaking-Anonymous-Session", Guid.NewGuid().ToString());
        return client;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] content,
        string contentType, int? durationSeconds = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", "upload");
        if (durationSeconds is not null)
            form.Add(new StringContent(durationSeconds.Value.ToString()), "durationSeconds");
        return url.Contains("/recordings/", StringComparison.Ordinal)
            ? client.PutAsync(url, form)
            : client.PostAsync(url, form);
    }

    private static async Task<JsonNode> JsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(),
            documentOptions: new JsonDocumentOptions { AllowTrailingCommas = false })!;
}
