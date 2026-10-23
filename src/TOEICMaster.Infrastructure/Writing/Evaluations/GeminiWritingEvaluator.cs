using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Writing.Evaluations;

namespace TOEICMaster.Infrastructure.Writing.Evaluations;

public sealed class GeminiWritingEvaluator(HttpClient httpClient, IOptions<GeminiOptions> options)
    : IWritingEvaluator
{
    private const string SystemInstruction = """
        You are a careful TOEIC Writing practice evaluator. Return only Vietnamese feedback for a learner.
        Score every answer using its supplied criterion scale, then give constructive feedback. The server converts
        those raw scores to a TOEIC Writing 0-200 estimate; it is never an official ETS result. Evaluate only the
        supplied prompt snapshot, private rubric snapshot, and learner answer. Do not claim to have
        accessed an image not represented by the text snapshot. Be constructive, concise, and do not reproduce a
        full model essay. Suggested revisions should be short examples, not a complete replacement answer.
        """;

    private static readonly JsonDocument OutputSchema = JsonDocument.Parse("""
        {
          "type":"object",
          "properties":{
            "summary":{"type":"string"},
            "strengths":{"type":"array","items":{"type":"string"},"maxItems":5},
            "improvements":{"type":"array","items":{"type":"string"},"maxItems":5},
            "feedback":{"type":"array","items":{"type":"object","properties":{
              "questionPosition":{"type":"integer"},"criterionScore":{"type":"integer","minimum":0,"maximum":5},"feedback":{"type":"string"},"suggestedRevision":{"type":"string"}
            },"required":["questionPosition","criterionScore","feedback","suggestedRevision"]}}
          },
          "required":["summary","strengths","improvements","feedback"]
        }
        """);

    public async Task<WritingEvaluatorResult> EvaluateAsync(WritingEvaluationWork work,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled || string.IsNullOrWhiteSpace(configuration.ApiKey))
            throw new WritingEvaluatorException("NOT_CONFIGURED",
                "Dịch vụ chấm AI chưa được cấu hình. Bài làm của bạn vẫn được lưu.", false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(work.Model)}:generateContent?key={Uri.EscapeDataString(configuration.ApiKey)}")
        {
            Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = SystemInstruction } } },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = BuildPrompt(work) } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    responseMimeType = "application/json",
                    responseJsonSchema = OutputSchema.RootElement
                }
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WritingEvaluatorException("TIMEOUT", "Chấm AI đang mất nhiều thời gian hơn dự kiến. Hệ thống sẽ thử lại.",
                true, innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new WritingEvaluatorException("NETWORK_ERROR", "Không thể kết nối dịch vụ chấm AI. Hệ thống sẽ thử lại.",
                true, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw ToProviderException(response);
            var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (payload is null)
                throw InvalidResponse();
            using (payload)
            {
                var text = ReadCandidateText(payload.RootElement);
                using var result = JsonDocument.Parse(text);
                return ParseResult(result.RootElement);
            }
        }
    }

    private static string BuildPrompt(WritingEvaluationWork work) => JsonSerializer.Serialize(new
    {
        rubricVersion = work.RubricVersion,
        answers = work.Answers.Select(answer => new
        {
            questionPosition = answer.Position,
            part = answer.Part,
            criterionMaximum = RawScoreMaximum(answer.Part),
            prompt = JsonSerializer.Deserialize<JsonElement>(answer.PromptSnapshotJson),
            privateRubric = JsonSerializer.Deserialize<JsonElement>(answer.ReviewSnapshotJson),
            learnerAnswer = answer.Answer
        })
    });

    private static string ReadCandidateText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0 || !candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            throw InvalidResponse();
        var text = parts.EnumerateArray().FirstOrDefault(part => part.TryGetProperty("text", out _));
        if (!text.TryGetProperty("text", out var value) || string.IsNullOrWhiteSpace(value.GetString()))
            throw InvalidResponse();
        return value.GetString()!;
    }

    private static WritingEvaluatorResult ParseResult(JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var summary) || string.IsNullOrWhiteSpace(summary.GetString()) ||
            !root.TryGetProperty("strengths", out var strengths) || strengths.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("improvements", out var improvements) || improvements.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("feedback", out var feedback) || feedback.ValueKind != JsonValueKind.Array)
            throw InvalidResponse();
        return new WritingEvaluatorResult(summary.GetString()!, ReadStrings(strengths),
            ReadStrings(improvements), feedback.EnumerateArray().Select(ReadFeedback).ToArray());
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement array)
    {
        var values = array.EnumerateArray().Select(item => item.GetString()).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace))
            throw InvalidResponse();
        return values!;
    }

    private static WritingEvaluationFeedbackResult ReadFeedback(JsonElement value)
    {
        if (!value.TryGetProperty("questionPosition", out var position) || !position.TryGetInt32(out var questionPosition) ||
            !value.TryGetProperty("criterionScore", out var score) || !score.TryGetInt32(out var criterionScore) ||
            !value.TryGetProperty("feedback", out var feedback) || string.IsNullOrWhiteSpace(feedback.GetString()) ||
            !value.TryGetProperty("suggestedRevision", out var suggestion) || string.IsNullOrWhiteSpace(suggestion.GetString()))
            throw InvalidResponse();
        return new WritingEvaluationFeedbackResult(questionPosition, criterionScore, feedback.GetString()!,
            suggestion.GetString()!);
    }

    private static WritingEvaluatorException ToProviderException(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500)
            return new WritingEvaluatorException(response.StatusCode == HttpStatusCode.TooManyRequests ? "QUOTA" : "PROVIDER_ERROR",
                "Dịch vụ chấm AI đang bận. Hệ thống sẽ thử lại.", true, retryAfter);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new WritingEvaluatorException("AUTHENTICATION", "Dịch vụ chấm AI chưa được cấu hình đúng.", false);
        return new WritingEvaluatorException("PROVIDER_REJECTED", "Dịch vụ chấm AI tạm thời không thể xử lý bài này.", false);
    }

    private static WritingEvaluatorException InvalidResponse() => new("INVALID_RESPONSE",
        "Dịch vụ chấm AI trả về kết quả không hợp lệ. Hệ thống sẽ thử lại.", true);

    private static int RawScoreMaximum(int part) => part switch
    {
        1 => 3,
        2 => 4,
        3 => 5,
        _ => throw new WritingEvaluatorException("INVALID_REQUEST", "Dữ liệu câu hỏi Writing không hợp lệ.", false)
    };
}

public sealed class DisabledWritingEvaluator : IWritingEvaluator
{
    public Task<WritingEvaluatorResult> EvaluateAsync(WritingEvaluationWork work, CancellationToken cancellationToken) =>
        throw new WritingEvaluatorException("NOT_CONFIGURED",
            "Dịch vụ chấm AI chưa được cấu hình. Bài làm của bạn vẫn được lưu.", false);
}

public sealed class GeminiWritingEvaluationSettings(IOptions<GeminiOptions> options) : IWritingEvaluationSettings
{
    public string Provider => "gemini";
    public string Model => options.Value.Model;
    public string RubricVersion => options.Value.RubricVersion;
}
