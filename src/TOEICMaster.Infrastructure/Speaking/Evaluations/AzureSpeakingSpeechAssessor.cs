using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Speaking.Evaluations;

/// <summary>
/// Azure Speech-to-text REST API for short audio with the pronunciation-assessment extension. Each answer is
/// converted to WAV and split into chunks of at most 30 seconds (the REST limit for pronunciation assessment);
/// chunk scores are combined weighted by duration. Answers are open-ended, so assessment runs unscripted.
/// </summary>
public sealed class AzureSpeakingSpeechAssessor(HttpClient httpClient, IOptions<AzureSpeechOptions> options,
    ISpeakingAudioConverter converter) : ISpeakingSpeechAssessor
{
    private const string WavContentType = "audio/wav; codecs=audio/pcm; samplerate=16000";
    private const double IssueThreshold = 60;
    private static readonly string[] NoSpeechStatuses = ["NoMatch", "InitialSilenceTimeout", "BabbleTimeout"];

    public async Task<SpeakingSpeechResult> AssessAsync(byte[] audio, string contentType, string locale,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!configuration.Enabled || string.IsNullOrWhiteSpace(configuration.Key))
            throw new SpeakingEvaluatorException("NOT_CONFIGURED",
                "Dịch vụ chấm AI Speaking chưa được cấu hình. Bài làm của bạn vẫn được lưu.", false);

        var chunks = await converter.ToWavChunksAsync(audio, contentType, cancellationToken);
        var results = new List<ChunkResult>();
        foreach (var chunk in chunks)
            results.Add(await AssessChunkAsync(chunk, locale, cancellationToken));
        return Combine(results);
    }

    private async Task<ChunkResult> AssessChunkAsync(SpeakingWavChunk chunk, string locale,
        CancellationToken cancellationToken)
    {
        var prosody = locale.Equals("en-US", StringComparison.OrdinalIgnoreCase);
        var (status, root) = await SendAsync(chunk, locale, Assessment(null, prosody), cancellationToken);
        if (status == HttpStatusCode.BadRequest)
        {
            // Some resources reject an assessment without reference text. Fall back to recognising the speech
            // first and assessing it against its own transcript, which is how unscripted assessment works.
            var (plainStatus, plain) = await SendAsync(chunk, locale, null, cancellationToken);
            EnsureSuccess(plainStatus);
            using (plain)
            {
                var recognized = Recognition(plain!.RootElement, chunk.Seconds, requireScores: false);
                if (!recognized.Recognized)
                    return recognized;
                (status, root) = await SendAsync(chunk, locale, Assessment(recognized.Lexical, prosody),
                    cancellationToken);
            }
        }
        EnsureSuccess(status);
        using (root)
            return Recognition(root!.RootElement, chunk.Seconds, requireScores: true);
    }

    private async Task<(HttpStatusCode Status, JsonDocument? Body)> SendAsync(SpeakingWavChunk chunk, string locale,
        string? assessmentHeader, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var uri = new Uri(configuration.SpeechToTextBaseUri(),
            "speech/recognition/conversation/cognitiveservices/v1?language=" + Uri.EscapeDataString(locale) +
            "&format=detailed");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Ocp-Apim-Subscription-Key", configuration.Key);
        request.Headers.Accept.ParseAdd("application/json");
        if (assessmentHeader is not null)
            request.Headers.Add("Pronunciation-Assessment", assessmentHeader);
        request.Content = new ByteArrayContent(chunk.Wav);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", WavContentType);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeakingEvaluatorException("SPEECH_TIMEOUT",
                "Nhận diện giọng nói đang mất nhiều thời gian hơn dự kiến. Hệ thống sẽ thử lại.", true,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SpeakingEvaluatorException("SPEECH_NETWORK_ERROR",
                "Không thể kết nối dịch vụ nhận diện giọng nói. Hệ thống sẽ thử lại.", true, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode != HttpStatusCode.BadRequest)
                    throw ToProviderException(response);
                return (response.StatusCode, null);
            }
            try
            {
                var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);
                return (response.StatusCode, body);
            }
            catch (JsonException exception)
            {
                throw InvalidResponse(exception);
            }
        }
    }

    /// <summary>Base64 JSON parameters of the Pronunciation-Assessment header.</summary>
    private static string Assessment(string? referenceText, bool prosody)
    {
        var parameters = new Dictionary<string, object>
        {
            ["GradingSystem"] = "HundredMark",
            ["Granularity"] = "Word",
            ["Dimension"] = "Comprehensive",
            ["EnableMiscue"] = false,
            ["EnableProsodyAssessment"] = prosody
        };
        if (referenceText is not null)
            parameters["ReferenceText"] = referenceText;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parameters)));
    }

    private static void EnsureSuccess(HttpStatusCode status)
    {
        if (status == HttpStatusCode.BadRequest)
            throw new SpeakingEvaluatorException("SPEECH_REJECTED",
                "Dịch vụ nhận diện giọng nói không xử lý được bản ghi này.", false);
    }

    internal static ChunkResult Recognition(JsonElement root, double seconds, bool requireScores)
    {
        var status = root.TryGetProperty("RecognitionStatus", out var value) ? value.GetString() : null;
        if (status is not null && NoSpeechStatuses.Contains(status))
            return ChunkResult.Silent(seconds);
        if (status != "Success")
            throw new SpeakingEvaluatorException("SPEECH_PROVIDER_ERROR",
                "Dịch vụ nhận diện giọng nói đang gặp sự cố. Hệ thống sẽ thử lại.", true);
        if (!root.TryGetProperty("NBest", out var candidates) || candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            return ChunkResult.Silent(seconds);

        var best = candidates[0];
        var display = Text(best, "Display") ?? Text(root, "DisplayText") ?? string.Empty;
        var lexical = Text(best, "Lexical") ?? display;
        if (string.IsNullOrWhiteSpace(display))
            return ChunkResult.Silent(seconds);
        var accuracy = Score(best, "AccuracyScore");
        var fluency = Score(best, "FluencyScore");
        var pronunciation = Score(best, "PronScore");
        if (requireScores && (accuracy is null || fluency is null || pronunciation is null))
            throw new SpeakingEvaluatorException("PRONUNCIATION_UNAVAILABLE",
                "Dịch vụ nhận diện giọng nói không trả về điểm phát âm. Hãy kiểm tra cấu hình Azure Speech.", false);

        var issues = new List<SpeakingWordIssue>();
        if (best.TryGetProperty("Words", out var words) && words.ValueKind == JsonValueKind.Array)
            foreach (var word in words.EnumerateArray())
            {
                var text = Text(word, "Word");
                var score = Score(word, "AccuracyScore");
                var errorType = Text(word, "ErrorType") ??
                                (word.TryGetProperty("PronunciationAssessment", out var assessment)
                                    ? Text(assessment, "ErrorType")
                                    : null);
                if (!string.IsNullOrWhiteSpace(text) && score is { } accuracyScore &&
                    (errorType == "Mispronunciation" || accuracyScore < IssueThreshold))
                    issues.Add(new SpeakingWordIssue(text, Math.Round(accuracyScore, 1)));
            }
        return new ChunkResult(true, display.Trim(), lexical.Trim(), seconds, accuracy ?? 0, fluency ?? 0,
            Score(best, "ProsodyScore"), pronunciation ?? 0, issues);
    }

    internal static SpeakingSpeechResult Combine(IReadOnlyList<ChunkResult> chunks)
    {
        var totalSeconds = Math.Round(chunks.Sum(chunk => chunk.Seconds), 2);
        var spoken = chunks.Where(chunk => chunk.Recognized).ToArray();
        if (spoken.Length == 0)
            return new SpeakingSpeechResult(SpeakingRecognitionStatus.NoSpeech, string.Empty, string.Empty,
                totalSeconds, 0, 0, null, 0, []);

        double Weighted(Func<ChunkResult, double> score) =>
            spoken.Sum(chunk => score(chunk) * chunk.Seconds) / spoken.Sum(chunk => chunk.Seconds);
        var withProsody = spoken.Where(chunk => chunk.Prosody is not null).ToArray();
        double? prosody = withProsody.Length == 0
            ? null
            : withProsody.Sum(chunk => chunk.Prosody!.Value * chunk.Seconds) / withProsody.Sum(chunk => chunk.Seconds);
        var issues = spoken.SelectMany(chunk => chunk.Issues).OrderBy(issue => issue.AccuracyScore)
            .DistinctBy(issue => issue.Word.ToLowerInvariant()).Take(20).ToArray();
        return new SpeakingSpeechResult(SpeakingRecognitionStatus.Recognized,
            string.Join(' ', spoken.Select(chunk => chunk.Display)), string.Join(' ', spoken.Select(chunk => chunk.Lexical)),
            totalSeconds, Weighted(chunk => chunk.Accuracy), Weighted(chunk => chunk.Fluency), prosody,
            Weighted(chunk => chunk.Pronunciation), issues);
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Reads a score placed either on the element or in its nested PronunciationAssessment object.</summary>
    private static double? Score(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.TryGetDouble(out var score))
            return score;
        return element.TryGetProperty("PronunciationAssessment", out var assessment) &&
               assessment.ValueKind == JsonValueKind.Object && assessment.TryGetProperty(name, out var nested) &&
               nested.TryGetDouble(out var nestedScore)
            ? nestedScore
            : null;
    }

    private static SpeakingEvaluatorException ToProviderException(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500)
            return new SpeakingEvaluatorException(
                response.StatusCode == HttpStatusCode.TooManyRequests ? "SPEECH_QUOTA" : "SPEECH_PROVIDER_ERROR",
                "Dịch vụ nhận diện giọng nói đang bận. Hệ thống sẽ thử lại.", true, response.Headers.RetryAfter?.Delta);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SpeakingEvaluatorException("SPEECH_AUTHENTICATION",
                "Dịch vụ nhận diện giọng nói chưa được cấu hình đúng.", false);
        return new SpeakingEvaluatorException("SPEECH_REJECTED",
            "Dịch vụ nhận diện giọng nói không xử lý được bản ghi này.", false);
    }

    private static SpeakingEvaluatorException InvalidResponse(Exception innerException) => new(
        "SPEECH_INVALID_RESPONSE", "Dịch vụ nhận diện giọng nói trả về kết quả không hợp lệ. Hệ thống sẽ thử lại.",
        true, innerException: innerException);

    internal sealed record ChunkResult(bool Recognized, string Display, string Lexical, double Seconds,
        double Accuracy, double Fluency, double? Prosody, double Pronunciation, IReadOnlyList<SpeakingWordIssue> Issues)
    {
        public static ChunkResult Silent(double seconds) => new(false, string.Empty, string.Empty, seconds, 0, 0,
            null, 0, []);
    }
}
