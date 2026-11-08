using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Writing;

public sealed class WritingLearnerFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Guest_can_start_save_submit_and_see_history_through_the_api_pipeline()
    {
        var now = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var actor = Guid.NewGuid();
        var exercise = WritingExercise.Create(WritingPart.Part3, "WP3-TEST", "Work", WritingDifficulty.Easy,
            WritingExerciseStatus.Published, "", "", null, null, null, null, null, null, null, null, null,
            "Explain your opinion.", 300, null, actor, now);
        var test = WritingTest.Create("WT-TEST", "Test", "", actor, now);
        test.SetInitialQuestions([WritingTestQuestion.CreateInline(test.Id, WritingPart.Part3, 1,
            JsonSerializer.Serialize(exercise.ToSnapshot()), null)]);
        test.Publish(now, actor);
        var repository = new InMemoryAttemptRepository(test, exercise);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWritingAttemptRepository>(repository);
            services.AddSingleton<IWritingEvaluationRepository, EmptyWritingEvaluationRepository>();
            services.AddSingleton<IWritingEvaluationSettings, StaticWritingEvaluationSettings>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Writing-Anonymous-Session", Guid.NewGuid().ToString());

        using var practiceResponse = await client.PostAsync($"/api/v1/writing/exercises/{exercise.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, practiceResponse.StatusCode);
        var practice = await practiceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practiceId = practice.GetProperty("id").GetGuid();
        using var save = await client.PutAsJsonAsync($"/api/v1/writing/attempts/{practiceId}/answers/1", new { answer = "My answer" });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var submit = await client.PostAsync($"/api/v1/writing/attempts/{practiceId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("SUBMITTED_AWAITING_EVALUATION", (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        using var testResponse = await client.PostAsJsonAsync("/api/v1/writing/attempts", new { testId = test.Id, mode = "FULL" });
        Assert.Equal(HttpStatusCode.Created, testResponse.StatusCode);
        var testAttempt = await testResponse.Content.ReadFromJsonAsync<JsonElement>();
        var testAttemptId = testAttempt.GetProperty("id").GetGuid();
        clock.Advance(120);
        using var next = await client.PostAsync($"/api/v1/writing/attempts/{testAttemptId}/next", null);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using var history = await client.GetAsync("/api/v1/writing/attempts");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(2, (await history.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength());
    }

    private sealed class InMemoryAttemptRepository(WritingTest test, WritingExercise exercise) : IWritingAttemptRepository
    {
        private readonly List<WritingAttempt> _attempts = [];
        public Task<WritingPublicTestPageDto> GetPublishedTestsAsync(string? query, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WritingPublicTestDto?> GetPublishedTestAsync(Guid testId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WritingTest?> FindPublishedTestForAttemptAsync(Guid testId, CancellationToken cancellationToken) => Task.FromResult(testId == test.Id ? test : null);
        public Task<WritingExercise?> FindPublishedExerciseAsync(Guid exerciseId, CancellationToken cancellationToken) => Task.FromResult(exerciseId == exercise.Id ? exercise : null);
        public Task<WritingPracticeExercisePageDto> GetPublishedExercisesAsync(int part, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WritingMediaAsset?> FindPublishedMediaAssetAsync(Guid assetId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WritingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken) => Task.FromResult(_attempts.SingleOrDefault(item => item.Id == attemptId));
        public Task<WritingAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken cancellationToken) => FindAttemptForUpdateAsync(attemptId, cancellationToken);
        public Task<IReadOnlyList<WritingAttempt>> GetAttemptsAsync(WritingAttemptOwner owner, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WritingAttempt>>(_attempts.Where(item => item.IsOwnedBy(owner.UserId, owner.AnonymousSessionHash)).ToArray());
        public Task AddAttemptAsync(WritingAttempt attempt, CancellationToken cancellationToken) { _attempts.Add(attempt); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
