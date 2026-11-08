using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Writing.Evaluations;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Infrastructure.Persistence.Writing;

public sealed class GeminiWritingEvaluatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Part_one_sends_its_image_and_only_sends_reference_when_enabled(bool includeReference)
    {
        var asset = WritingMediaAsset.Create("image-key", "picture.png", "image/png", 3, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var http = new CaptureHandler();
        var evaluator = new GeminiWritingEvaluator(new HttpClient(http) { BaseAddress = new Uri("https://example.test/") },
            Options.Create(new GeminiOptions { Enabled = true, ApiKey = "test", UseWritingReferenceAnswers = includeReference }),
            new EmptyWritingAdminRepository { MediaAsset = asset }, new EmptyWritingImageStore { Data = [1, 2, 3] });
        var work = new WritingEvaluationWork(Guid.NewGuid(), "gemini", "test-model", "v1",
            [new WritingEvaluationAnswer(1, 1, $"{{\"Part\":1,\"ImageAssetId\":\"{asset.Id}\"}}",
                "{\"ModelAnswer\":\"REFERENCE_SECRET\",\"RubricNotes\":\"Check keywords\"}", "My answer")]);

        var result = await evaluator.EvaluateAsync(work, CancellationToken.None);

        Assert.Equal("Feedback", result.Summary);
        Assert.Contains("AQID", http.Body, StringComparison.Ordinal);
        Assert.Contains("image/png", http.Body, StringComparison.Ordinal);
        Assert.Equal("test", http.ApiKey);
        Assert.DoesNotContain("test", http.Query, StringComparison.Ordinal);
        Assert.Equal(includeReference, http.Body.Contains("REFERENCE_SECRET", StringComparison.Ordinal));
        Assert.Contains("Check keywords", http.Body, StringComparison.Ordinal);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string? ApiKey { get; private set; }
        public string Query { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            Query = request.RequestUri!.Query;
            var feedback = JsonSerializer.Serialize(new
            {
                summary = "Feedback",
                strengths = Array.Empty<string>(),
                improvements = Array.Empty<string>(),
                feedback = new[] { new { questionPosition = 1, criterionScore = 2, feedback = "Feedback", suggestedRevision = "Example" } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[] { new { content = new { parts = new[] { new { text = feedback } } } } }
                }))
            };
        }
    }
}
