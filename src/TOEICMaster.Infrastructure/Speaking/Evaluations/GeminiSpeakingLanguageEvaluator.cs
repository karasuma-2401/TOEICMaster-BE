using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Infrastructure.Writing.Evaluations;

namespace TOEICMaster.Infrastructure.Speaking.Evaluations;

/// <summary>Grades grammar, vocabulary and relevance of Azure Speech transcripts with the team's Gemini key.</summary>
public sealed class GeminiSpeakingLanguageEvaluator(HttpClient httpClient, IOptions<GeminiOptions> options)
    : ISpeakingLanguageEvaluator
{
    private const string SystemInstruction = """
        You are a careful TOEIC Speaking practice evaluator. Write all feedback in Vietnamese for a learner.
        Each answer is an automatic Azure Speech transcript of a spoken response, with Azure pronunciation scores
        (0-100). Transcripts can contain recognition errors and have no reliable punctuation: do not penalise
        grammar for missing punctuation or for an isolated word that is clearly a recognition error.
        For every answer return integers from 0 to 100:
        - grammarScore: grammatical accuracy and range of the spoken sentences.
        - vocabularyScore: precision, range and appropriacy of word choice for the task.
        - relevanceScore: how completely and appropriately the answer addresses the task (and, for Part 3/4, the
          question selected by questionIndex), including cohesion and enough supporting detail.
        TOEIC Speaking parts: 1 = read the passage aloud; 2 = describe the picture (you only get its text
        description, never claim to see the image); 3 = respond to questions (questions[questionIndex]); 4 =
        respond using the information document (questions[questionIndex]); 5 = express and support an opinion.
        For Part 1 return 0 for grammarScore, vocabularyScore and relevanceScore, give pronunciation and reading
        feedback using the mispronounced words, and return an empty suggestedResponse.
        Use the private rubric notes and model answer only as guidance; never quote the model answer in full.
        feedback: 2-4 concise sentences. suggestedResponse: a short improved version (at most 3 sentences).
        The server converts the scores into a TOEIC Speaking 0-200 estimate; it is never an official ETS result.
        """;

    private static readonly JsonDocument OutputSchema = JsonDocument.Parse("""
        {
          "type":"object",
          "properties":{
            "summary":{"type":"string"},
            "strengths":{"type":"array","items":{"type":"string"},"maxItems":5},
            "improvements":{"type":"array","items":{"type":"string"},"maxItems":5},
            "items":{"type":"array","items":{"type":"object","properties":{
              "position":{"type":"integer"},
              "grammarScore":{"type":"integer","minimum":0,"maximum":100},
              "vocabularyScore":{"type":"integer","minimum":0,"maximum":100},
              "relevanceScore":{"type":"integer","minimum":0,"maximum":100},
              "feedback":{"type":"string"},
              "suggestedResponse":{"type":"string"}
            },"required":["position","grammarScore","vocabularyScore","relevanceScore","feedback","suggestedResponse"]}}
          },
          "required":["summary","strengths","improvements","items"]
        }
        """);

    public async Task<SpeakingLanguageResult> EvaluateAsync(SpeakingLanguageWork work,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled || string.IsNullOrWhiteSpace(configuration.ApiKey))
            throw new SpeakingEvaluatorException("NOT_CONFIGURED",
                "Dịch vụ chấm AI Speaking chưa được cấu hình. Bài làm của bạn vẫn được lưu.", false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(work.Model)}:generateContent");
        request.Headers.Add("x-goog-api-key", configuration.ApiKey);
        request.Content = JsonContent.Create(new
        {
            systemInstruction = new { parts = new[] { new { text = SystemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = BuildPrompt(work) } } } },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                responseJsonSchema = OutputSchema.RootElement
            }
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeakingEvaluatorException("TIMEOUT",
                "Chấm AI đang mất nhiều thời gian hơn dự kiến. Hệ thống sẽ thử lại.", true, innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SpeakingEvaluatorException("NETWORK_ERROR",
                "Không thể kết nối dịch vụ chấm AI. Hệ thống sẽ thử lại.", true, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw ToProviderException(response);
            try
            {
                using var payload = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                using var result = JsonDocument.Parse(CandidateText(payload.RootElement));
                return Parse(result.RootElement);
            }
            catch (JsonException exception)
            {
                throw InvalidResponse(exception);
            }
        }
    }

    private static string BuildPrompt(SpeakingLanguageWork work) => JsonSerializer.Serialize(new
    {
        rubricVersion = work.RubricVersion,
        answers = work.Items.Select(item => new
        {
            position = item.Position,
            part = item.Part,
            questionIndex = item.QuestionIndex,
            task = JsonSerializer.Deserialize<JsonElement>(item.ContentJson),
            privateRubric = JsonSerializer.Deserialize<JsonElement>(item.ReviewJson),
            transcript = item.Transcript,
            pronunciation = new
            {
                pronunciationScore = item.PronunciationScore,
                fluencyScore = item.FluencyScore,
                prosodyScore = item.ProsodyScore,
                completenessScore = item.CompletenessScore,
                mispronouncedWords = item.WordIssues.Select(issue => issue.Word)
            }
        })
    }, SpeakingJson.Options);

    internal static string CandidateText(JsonElement root)
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

    internal static SpeakingLanguageResult Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || Text(root, "summary") is not { } summary ||
            !root.TryGetProperty("strengths", out var strengths) || strengths.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("improvements", out var improvements) || improvements.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw InvalidResponse();
        return new SpeakingLanguageResult(summary, Strings(strengths), Strings(improvements),
            items.EnumerateArray().Select(ParseItem).ToArray());
    }

    private static SpeakingLanguageItemResult ParseItem(JsonElement item)
    {
        if (!Integer(item, "position", out var position) || !Integer(item, "grammarScore", out var grammar) ||
            !Integer(item, "vocabularyScore", out var vocabulary) ||
            !Integer(item, "relevanceScore", out var relevance) || Text(item, "feedback") is not { } feedback ||
            !item.TryGetProperty("suggestedResponse", out var suggestion) ||
            suggestion.ValueKind != JsonValueKind.String)
            throw InvalidResponse();
        return new SpeakingLanguageItemResult(position, grammar, vocabulary, relevance, feedback,
            suggestion.GetString()!);
    }

    private static bool Integer(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) &&
               property.TryGetInt32(out value);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> Strings(JsonElement array)
    {
        var values = array.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace))
            throw InvalidResponse();
        return values!;
    }

    private static SpeakingEvaluatorException ToProviderException(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500)
            return new SpeakingEvaluatorException(
                response.StatusCode == HttpStatusCode.TooManyRequests ? "QUOTA" : "PROVIDER_ERROR",
                "Dịch vụ chấm AI đang bận. Hệ thống sẽ thử lại.", true, response.Headers.RetryAfter?.Delta);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SpeakingEvaluatorException("AUTHENTICATION", "Dịch vụ chấm AI chưa được cấu hình đúng.", false);
        return new SpeakingEvaluatorException("PROVIDER_REJECTED",
            "Dịch vụ chấm AI tạm thời không thể xử lý bài này.", false);
    }

    private static SpeakingEvaluatorException InvalidResponse(Exception? innerException = null) => new(
        "INVALID_RESPONSE", "Dịch vụ chấm AI trả về kết quả không hợp lệ. Hệ thống sẽ thử lại.", true,
        innerException: innerException);
}
