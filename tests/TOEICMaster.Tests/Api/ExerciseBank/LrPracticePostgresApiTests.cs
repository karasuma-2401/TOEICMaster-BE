using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.ExerciseBank;

public sealed class LrPracticePostgresApiFactory : ApiFactory
{
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
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "LrPracticePostgresTest";
                    options.DefaultChallengeScheme = "LrPracticePostgresTest";
                    options.DefaultForbidScheme = "LrPracticePostgresTest";
                })
                .AddScheme<AuthenticationSchemeOptions, LrPracticeTestAuthenticationHandler>(
                    "LrPracticePostgresTest", _ => { });
        });
    }
}

public sealed class LrPracticePostgresApiTests : IClassFixture<LrPracticePostgresApiFactory>
{
    private readonly LrPracticePostgresApiFactory _factory;

    public LrPracticePostgresApiTests(LrPracticePostgresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Endpoints_enforce_published_visibility_persistence_and_user_isolation_in_postgresql()
    {
        var now = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userA = User.CreateExternal($"Learner A {suffix}", $"lr-api-a-{suffix}@example.com",
            $"LR-API-A-{suffix}@EXAMPLE.COM", now);
        var userB = User.CreateExternal($"Learner B {suffix}", $"lr-api-b-{suffix}@example.com",
            $"LR-API-B-{suffix}@EXAMPLE.COM", now);

        var publishedPart4 = LrExerciseBankItem.Create(
            LrExercisePart.Part4,
            $"P4-E2E-{suffix}",
            "Announcement",
            LrExerciseDifficulty.Medium,
            LrExerciseStatus.Published,
            "Welcome aboard flight 204.",
            "https://example.test/announcement.wav",
            null,
            [
                new LrExerciseBankQuestionInput($"P4-E2E-{suffix}-Q1", "Q1", "A1", "B1", "C1", "D1", "A", "T1", "E1"),
                new LrExerciseBankQuestionInput($"P4-E2E-{suffix}-Q2", "Q2", "A2", "B2", "C2", "D2", "B", "T2", "E2"),
                new LrExerciseBankQuestionInput($"P4-E2E-{suffix}-Q3", "Q3", "A3", "B3", "C3", "D3", "C", "T3", "E3")
            ],
            userA.Id,
            now);

        var draftPart4 = LrExerciseBankItem.Create(
            LrExercisePart.Part4,
            $"P4-DRAFT-{suffix}",
            "Draft Announcement",
            LrExerciseDifficulty.Easy,
            LrExerciseStatus.Draft,
            "Draft speech.",
            null,
            null,
            [
                new LrExerciseBankQuestionInput($"P4-DRF-{suffix}-Q1", "Q1", "A1", "B1", "C1", "D1", "A", null, null),
                new LrExerciseBankQuestionInput($"P4-DRF-{suffix}-Q2", "Q2", "A2", "B2", "C2", "D2", "B", null, null),
                new LrExerciseBankQuestionInput($"P4-DRF-{suffix}-Q3", "Q3", "A3", "B3", "C3", "D3", "C", null, null)
            ],
            userA.Id,
            now);

        var publishedPart5 = LrExerciseBankItem.Create(
            LrExercisePart.Part5,
            $"P5-E2E-{suffix}",
            "Grammar",
            LrExerciseDifficulty.Easy,
            LrExerciseStatus.Published,
            null,
            null,
            null,
            [
                new LrExerciseBankQuestionInput($"P5-E2E-{suffix}-Q1", "Q1", "A1", "B1", "C1", "D1", "D", "T5", "E5")
            ],
            userA.Id,
            now);

        var legacyPart1WithoutAudio = LrExerciseBankItem.Create(
            LrExercisePart.Part1, $"P1-LEGACY-{suffix}", "Legacy photo", LrExerciseDifficulty.Easy,
            LrExerciseStatus.Published, null, "https://example.test/audio.mp3",
            "https://example.test/photo.png",
            [new LrExerciseBankQuestionInput($"P1-LEGACY-{suffix}-Q1", "Q1", "A", "B", "C", "D", "A", null, null)],
            userA.Id, now);
        var legacyPart1WithoutImage = LrExerciseBankItem.Create(
            LrExercisePart.Part1, $"P1-LEGACY-IMAGE-{suffix}", "Legacy photo", LrExerciseDifficulty.Easy,
            LrExerciseStatus.Published, null, "https://example.test/audio.mp3",
            "https://example.test/photo.png",
            [new LrExerciseBankQuestionInput($"P1-LEGACY-IMAGE-{suffix}-Q1", "Q1", "A", "B", "C", "D", "A", null, null)],
            userA.Id, now);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            db.Users.AddRange(userA, userB);
            db.LrExerciseBankItems.AddRange(publishedPart4, draftPart4, publishedPart5,
                legacyPart1WithoutAudio, legacyPart1WithoutImage);
            db.Entry(legacyPart1WithoutAudio).Property(item => item.AudioUrl).CurrentValue = "   ";
            db.Entry(legacyPart1WithoutImage).Property(item => item.ImageUrl).CurrentValue = "   ";
            await db.SaveChangesAsync();
        }

        using var anonClient = _factory.CreateClient();

        using var legacyList = await anonClient.GetAsync("/api/v1/lr-practice/parts/1/items");
        var legacyPage = await legacyList.Content.ReadFromJsonAsync<LrPracticeItemPageDto>();
        Assert.DoesNotContain(legacyPage!.Items, item => item.Id == legacyPart1WithoutAudio.Id);
        Assert.DoesNotContain(legacyPage.Items, item => item.Id == legacyPart1WithoutImage.Id);
        using var legacyDetail = await anonClient.GetAsync($"/api/v1/lr-practice/items/{legacyPart1WithoutAudio.Id}");
        Assert.Equal(HttpStatusCode.NotFound, legacyDetail.StatusCode);
        using var legacyImageDetail = await anonClient.GetAsync($"/api/v1/lr-practice/items/{legacyPart1WithoutImage.Id}");
        Assert.Equal(HttpStatusCode.NotFound, legacyImageDetail.StatusCode);

        using var listResponse = await anonClient.GetAsync("/api/v1/lr-practice/parts/4/items?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listPage = await listResponse.Content.ReadFromJsonAsync<LrPracticeItemPageDto>();
        Assert.NotNull(listPage);
        Assert.Contains(listPage.Items, item => item.Id == publishedPart4.Id);
        Assert.DoesNotContain(listPage.Items, item => item.Id == draftPart4.Id);
        Assert.DoesNotContain(listPage.Items, item => item.Id == publishedPart5.Id);

        using var draftDetailResponse = await anonClient.GetAsync($"/api/v1/lr-practice/items/{draftPart4.Id}");
        Assert.Equal(HttpStatusCode.NotFound, draftDetailResponse.StatusCode);
        Assert.Equal("application/problem+json", draftDetailResponse.Content.Headers.ContentType?.MediaType);

        using var detailResponse = await anonClient.GetAsync($"/api/v1/lr-practice/items/{publishedPart4.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailJson = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(detailJson.GetProperty("questions")[0].TryGetProperty("correctKey", out _));

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-User", userA.Id.ToString());
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-User", userB.Id.ToString());

        using var draftSubmitResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{draftPart4.Id}/submit",
            new { answers = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.NotFound, draftSubmitResponse.StatusCode);
        Assert.Equal("application/problem+json", draftSubmitResponse.Content.Headers.ContentType?.MediaType);

        var p4Questions = publishedPart4.Questions.OrderBy(q => q.Ordinal).ToArray();
        var p5Question = publishedPart5.Questions.Single();

        using var submitA1 = await clientA.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{publishedPart4.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = p4Questions[0].Id, selectedKey = "A" },
                    new { questionId = p4Questions[1].Id, selectedKey = "D" }
                }
            });
        Assert.Equal(HttpStatusCode.OK, submitA1.StatusCode);
        var resultA1 = await submitA1.Content.ReadFromJsonAsync<LrPracticeSubmitResultDto>();
        Assert.NotNull(resultA1);
        Assert.Equal(1, resultA1.CorrectCount);

        using var submitA2 = await clientA.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{publishedPart4.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = p4Questions[0].Id, selectedKey = "A" },
                    new { questionId = p4Questions[1].Id, selectedKey = "B" },
                    new { questionId = p4Questions[2].Id, selectedKey = "C" }
                }
            });
        Assert.Equal(HttpStatusCode.OK, submitA2.StatusCode);

        using var submitB1 = await clientB.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{publishedPart4.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = p4Questions[0].Id, selectedKey = "A" },
                    new { questionId = p4Questions[1].Id, selectedKey = "B" },
                    new { questionId = p4Questions[2].Id, selectedKey = "D" }
                }
            });
        Assert.Equal(HttpStatusCode.OK, submitB1.StatusCode);

        using var submitAPart5 = await clientA.PostAsJsonAsync(
            $"/api/v1/lr-practice/items/{publishedPart5.Id}/submit",
            new
            {
                answers = new[]
                {
                    new { questionId = p5Question.Id, selectedKey = "D" }
                }
            });
        Assert.Equal(HttpStatusCode.OK, submitAPart5.StatusCode);

        using var statsAResponse = await clientA.GetAsync("/api/v1/lr-practice/parts/4/statistics");
        Assert.Equal(HttpStatusCode.OK, statsAResponse.StatusCode);
        var statsA = await statsAResponse.Content.ReadFromJsonAsync<List<LrPracticeItemStatisticsDto>>();
        Assert.NotNull(statsA);
        var itemStatA = Assert.Single(statsA);
        Assert.Equal(publishedPart4.Id, itemStatA.ItemId);
        Assert.Equal(2, itemStatA.AttemptCount);
        Assert.Equal(3, itemStatA.BestScore);
        Assert.Equal(3, itemStatA.TotalQuestions);

        using var statsBResponse = await clientB.GetAsync("/api/v1/lr-practice/parts/4/statistics");
        Assert.Equal(HttpStatusCode.OK, statsBResponse.StatusCode);
        var statsB = await statsBResponse.Content.ReadFromJsonAsync<List<LrPracticeItemStatisticsDto>>();
        Assert.NotNull(statsB);
        var itemStatB = Assert.Single(statsB);
        Assert.Equal(publishedPart4.Id, itemStatB.ItemId);
        Assert.Equal(1, itemStatB.AttemptCount);
        Assert.Equal(2, itemStatB.BestScore);
        Assert.Equal(3, itemStatB.TotalQuestions);
    }
}
