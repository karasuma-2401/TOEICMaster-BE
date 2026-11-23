using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Infrastructure.Pronunciation;

/// <summary>
/// Scripted, phoneme-level Azure Pronunciation Assessment of one example word. Same request flow as the Speaking
/// assessor (speech-to-text REST API for short audio, 16 kHz WAV, <c>Pronunciation-Assessment</c> header, retry
/// with simpler parameters on 400), but with <c>ReferenceText</c> and <c>Granularity=Phoneme</c>. Phonemes come
/// back in the IPA alphabet with the five most likely candidates each.
/// </summary>
public sealed class AzurePronunciationAssessor(HttpClient httpClient, IOptions<PronunciationSpeechOptions> options,
    IPronunciationAudioConverter converter) : IPronunciationAssessor
{
    private const string WavContentType = "audio/wav; codecs=audio/pcm; samplerate=16000";
    private const double MinimumSeconds = 0.2;
    private static readonly string[] NoSpeechStatuses = ["NoMatch", "InitialSilenceTimeout", "BabbleTimeout"];

    public async Task<PronunciationAssessmentDto> AssessAsync(byte[] audio, string contentType,
        string referenceText, string locale, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!configuration.IsConfigured)
            throw PronunciationAssessmentException.NotConfigured();

        var wav = await converter.ToWavAsync(audio, contentType, cancellationToken);
        if (wav.Seconds > PronunciationRules.MaxAudioSeconds + 0.5)
            throw PronunciationAssessmentException.InvalidAudio("Recording is longer than 10 seconds. Please record a shorter clip.");
        if (wav.Seconds < MinimumSeconds)
            return PronunciationAssessmentDto.NoSpeech(referenceText);

        var (status, document) = await SendAsync(wav.Wav, locale, AssessmentHeader(referenceText, ipa: true),
            cancellationToken);
        if (status == HttpStatusCode.BadRequest)
        {
            // Like Speaking's fallback: some resources reject part of the parameters (here the IPA alphabet or
            // phoneme candidates). Retry once with default phoneme symbols; the word scores still come back.
            (status, document) = await SendAsync(wav.Wav, locale, AssessmentHeader(referenceText, ipa: false),
                cancellationToken);
            if (status == HttpStatusCode.BadRequest)
                throw PronunciationAssessmentException.Rejected();
        }
        using (document)
            return Parse(document!.RootElement, referenceText);
    }

    private async Task<(HttpStatusCode Status, JsonDocument? Body)> SendAsync(byte[] wav, string locale,
        string assessmentHeader, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var uri = new Uri(configuration.SpeechToTextBaseUri(),
            "speech/recognition/conversation/cognitiveservices/v1?language=" + Uri.EscapeDataString(locale) +
            "&format=detailed");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Ocp-Apim-Subscription-Key", configuration.Key);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("Pronunciation-Assessment", assessmentHeader);
        request.Content = new ByteArrayContent(wav);
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
            throw PronunciationAssessmentException.Timeout(exception);
        }
        catch (HttpRequestException exception)
        {
            throw PronunciationAssessmentException.ProviderError(exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return (response.StatusCode, null);
            if (!response.IsSuccessStatusCode)
                throw response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? PronunciationAssessmentException.NotConfigured()
                    : PronunciationAssessmentException.ProviderError();
            try
            {
                return (response.StatusCode, await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken));
            }
            catch (JsonException exception)
            {
                throw PronunciationAssessmentException.ProviderError(exception);
            }
        }
    }

    /// <summary>Base64 JSON parameters of the Pronunciation-Assessment header.</summary>
    internal static string AssessmentHeader(string referenceText, bool ipa = true)
    {
        var parameters = new Dictionary<string, object>
        {
            ["ReferenceText"] = referenceText,
            ["GradingSystem"] = "HundredMark",
            ["Granularity"] = "Phoneme",
            ["Dimension"] = "Comprehensive",
            ["EnableMiscue"] = false
        };
        if (ipa)
        {
            parameters["PhonemeAlphabet"] = "IPA";
            parameters["NBestPhonemeCount"] = 5;
        }
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parameters)));
    }

    internal static PronunciationAssessmentDto Parse(JsonElement root, string referenceText)
    {
        var status = Text(root, "RecognitionStatus");
        if (status is not null && NoSpeechStatuses.Contains(status))
            return PronunciationAssessmentDto.NoSpeech(referenceText);
        if (status != "Success")
            throw PronunciationAssessmentException.ProviderError();
        if (!root.TryGetProperty("NBest", out var candidates) || candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            return PronunciationAssessmentDto.NoSpeech(referenceText);

        var best = candidates[0];
        var recognized = (Text(best, "Display") ?? Text(root, "DisplayText") ?? string.Empty).Trim();
        var accuracy = Score(best, "AccuracyScore");
        var pronunciation = Score(best, "PronScore");
        if (accuracy is null || pronunciation is null)
            throw PronunciationAssessmentException.ProviderError();

        var words = new List<PronunciationWordDto>();
        if (best.TryGetProperty("Words", out var wordElements) && wordElements.ValueKind == JsonValueKind.Array)
            foreach (var word in wordElements.EnumerateArray())
            {
                var text = Text(word, "Word");
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var errorType = Text(word, "ErrorType") ?? Text(Nested(word), "ErrorType") ?? "None";
                words.Add(new PronunciationWordDto(text, Clamp(Score(word, "AccuracyScore") ?? 0), errorType,
                    Phonemes(word)));
            }

        return new PronunciationAssessmentDto(PronunciationRecognitionStatus.Recognized, referenceText, recognized,
            Clamp(accuracy.Value), Clamp(pronunciation.Value), Clamp(Score(best, "CompletenessScore") ?? 0), words);
    }

    private static List<PronunciationPhonemeDto> Phonemes(JsonElement word)
    {
        var phonemes = new List<PronunciationPhonemeDto>();
        if (!word.TryGetProperty("Phonemes", out var elements) || elements.ValueKind != JsonValueKind.Array)
            return phonemes;
        foreach (var phoneme in elements.EnumerateArray())
        {
            var symbol = Text(phoneme, "Phoneme");
            if (string.IsNullOrWhiteSpace(symbol))
                continue;
            var candidates = new List<PronunciationPhonemeCandidateDto>();
            var nBest = phoneme.TryGetProperty("NBestPhonemes", out var direct)
                ? direct
                : Nested(phoneme).ValueKind == JsonValueKind.Object &&
                  Nested(phoneme).TryGetProperty("NBestPhonemes", out var nested)
                    ? nested
                    : default;
            if (nBest.ValueKind == JsonValueKind.Array)
                foreach (var candidate in nBest.EnumerateArray())
                {
                    var candidateSymbol = Text(candidate, "Phoneme");
                    if (!string.IsNullOrWhiteSpace(candidateSymbol) &&
                        candidate.TryGetProperty("Score", out var score) && score.TryGetDouble(out var value))
                        candidates.Add(new PronunciationPhonemeCandidateDto(candidateSymbol, Clamp(value)));
                }
            phonemes.Add(new PronunciationPhonemeDto(symbol, Clamp(Score(phoneme, "AccuracyScore") ?? 0),
                candidates));
        }
        return phonemes;
    }

    private static double Clamp(double value) => Math.Round(Math.Clamp(value, 0, 100), 1);

    private static JsonElement Nested(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty("PronunciationAssessment", out var nested) && nested.ValueKind == JsonValueKind.Object
            ? nested
            : default;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Reads a score placed on the element or in its nested PronunciationAssessment object.</summary>
    private static double? Score(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.TryGetDouble(out var score))
            return score;
        var nested = Nested(element);
        return nested.ValueKind == JsonValueKind.Object && nested.TryGetProperty(name, out var inner) &&
               inner.TryGetDouble(out var nestedScore)
            ? nestedScore
            : null;
    }
}
