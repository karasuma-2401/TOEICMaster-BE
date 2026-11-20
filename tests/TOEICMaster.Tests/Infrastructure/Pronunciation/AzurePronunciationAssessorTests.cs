using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Pronunciation;
using TOEICMaster.Infrastructure.Pronunciation;

namespace TOEICMaster.Tests.Infrastructure.Pronunciation;

public sealed class AzurePronunciationAssessorTests
{
    private static readonly PronunciationSpeechOptions Azure = new()
    {
        Enabled = true,
        Key = "speech-key",
        Region = "southeastasia"
    };

    private const string Think = """
        {
          "RecognitionStatus": "Success",
          "DisplayText": "Think.",
          "NBest": [{
            "Display": "Think.",
            "AccuracyScore": 78, "PronScore": 80.46, "CompletenessScore": 100,
            "Words": [{
              "Word": "think", "AccuracyScore": 78, "ErrorType": "None",
              "Phonemes": [
                { "Phoneme": "θ", "AccuracyScore": 54,
                  "NBestPhonemes": [ { "Phoneme": "s", "Score": 71 }, { "Phoneme": "θ", "Score": 54 } ] },
                { "Phoneme": "ɪ", "AccuracyScore": 92 },
                { "Phoneme": "ŋ", "AccuracyScore": 88 },
                { "Phoneme": "k", "AccuracyScore": 95 }
              ]
            }]
          }]
        }
        """;

    [Fact]
    public async Task Requests_scripted_phoneme_assessment_in_ipa_on_converted_wav()
    {
        var handler = new StubHandler(_ => Json(Think));
        var converter = new StubConverter(new PronunciationWav([1, 2, 3], 1.2));
        var assessor = Assessor(handler, converter);

        var result = await assessor.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None);

        Assert.Equal("audio/webm", converter.ContentType);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://southeastasia.stt.speech.microsoft.com/speech/recognition/conversation/" +
                     "cognitiveservices/v1?language=en-US&format=detailed", request.Uri.ToString());
        Assert.Equal("speech-key", request.Headers["Ocp-Apim-Subscription-Key"]);
        Assert.Equal([1, 2, 3], request.Body);
        using var header = JsonDocument.Parse(Encoding.UTF8.GetString(
            Convert.FromBase64String(request.Headers["Pronunciation-Assessment"])));
        var parameters = header.RootElement;
        Assert.Equal("think", parameters.GetProperty("ReferenceText").GetString());
        Assert.Equal("Phoneme", parameters.GetProperty("Granularity").GetString());
        Assert.Equal("IPA", parameters.GetProperty("PhonemeAlphabet").GetString());
        Assert.Equal(5, parameters.GetProperty("NBestPhonemeCount").GetInt32());
        Assert.Equal("HundredMark", parameters.GetProperty("GradingSystem").GetString());

        Assert.Equal(PronunciationRecognitionStatus.Recognized, result.RecognitionStatus);
        Assert.Equal(("think", "Think.", 78d, 80.5d, 100d),
            (result.ReferenceText, result.RecognizedText, result.AccuracyScore, result.PronunciationScore,
                result.CompletenessScore));
        var word = Assert.Single(result.Words);
        Assert.Equal(["θ", "ɪ", "ŋ", "k"], word.Phonemes.Select(phoneme => phoneme.Phoneme));
        Assert.Equal([new PronunciationPhonemeCandidateDto("s", 71), new PronunciationPhonemeCandidateDto("θ", 54)],
            word.Phonemes[0].Candidates);
        Assert.Empty(word.Phonemes[1].Candidates);
    }

    [Fact]
    public void Reads_scores_and_candidates_nested_under_PronunciationAssessment()
    {
        using var document = JsonDocument.Parse("""
            {
              "RecognitionStatus": "Success",
              "NBest": [{
                "Display": "This.",
                "PronunciationAssessment": { "AccuracyScore": 60, "PronScore": 61, "CompletenessScore": 100 },
                "Words": [{
                  "Word": "this",
                  "PronunciationAssessment": { "AccuracyScore": 60, "ErrorType": "Mispronunciation" },
                  "Phonemes": [{
                    "Phoneme": "ð",
                    "PronunciationAssessment": {
                      "AccuracyScore": 40,
                      "NBestPhonemes": [ { "Phoneme": "d", "Score": 80 } ]
                    }
                  }]
                }]
              }]
            }
            """);

        var result = AzurePronunciationAssessor.Parse(document.RootElement, "this");

        var word = Assert.Single(result.Words);
        Assert.Equal(("Mispronunciation", 60d), (word.ErrorType, word.AccuracyScore));
        var phoneme = Assert.Single(word.Phonemes);
        Assert.Equal(40, phoneme.AccuracyScore);
        Assert.Equal("d", Assert.Single(phoneme.Candidates).Phoneme);
    }

    [Theory]
    [InlineData("""{"RecognitionStatus":"NoMatch"}""")]
    [InlineData("""{"RecognitionStatus":"InitialSilenceTimeout"}""")]
    [InlineData("""{"RecognitionStatus":"Success","NBest":[]}""")]
    public void Silence_becomes_NoSpeech(string body)
    {
        using var document = JsonDocument.Parse(body);

        var result = AzurePronunciationAssessor.Parse(document.RootElement, "think");

        Assert.Equal(PronunciationRecognitionStatus.NoSpeech, result.RecognitionStatus);
        Assert.Equal((0d, 0d, 0d), (result.AccuracyScore, result.PronunciationScore, result.CompletenessScore));
        Assert.Empty(result.Words);
    }

    [Fact]
    public async Task Unconfigured_service_fails_before_converting_or_calling_azure()
    {
        var handler = new StubHandler(_ => Json(Think));
        var converter = new StubConverter(new PronunciationWav([1], 1));
        var assessor = new AzurePronunciationAssessor(new HttpClient(handler),
            Options.Create(new PronunciationSpeechOptions { Enabled = false }), converter);

        var error = await Assert.ThrowsAsync<PronunciationAssessmentException>(() =>
            assessor.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None));

        Assert.Equal((PronunciationFailureKind.NotConfigured, "NOT_CONFIGURED"), (error.Kind, error.Code));
        Assert.Null(converter.ContentType);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Recordings_longer_than_ten_seconds_are_rejected_without_calling_azure()
    {
        var handler = new StubHandler(_ => Json(Think));
        var assessor = Assessor(handler, new StubConverter(new PronunciationWav([1], 11)));

        var error = await Assert.ThrowsAsync<PronunciationAssessmentException>(() =>
            assessor.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None));

        Assert.Equal(PronunciationFailureKind.InvalidAudio, error.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Near_empty_recordings_return_NoSpeech_without_calling_azure()
    {
        var handler = new StubHandler(_ => Json(Think));
        var assessor = Assessor(handler, new StubConverter(new PronunciationWav([1], 0.1)));

        var result = await assessor.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None);

        Assert.Equal(PronunciationRecognitionStatus.NoSpeech, result.RecognitionStatus);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, PronunciationFailureKind.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, PronunciationFailureKind.NotConfigured)]
    [InlineData(HttpStatusCode.TooManyRequests, PronunciationFailureKind.ProviderError)]
    [InlineData(HttpStatusCode.InternalServerError, PronunciationFailureKind.ProviderError)]
    public async Task Provider_errors_map_to_safe_failures(HttpStatusCode status, PronunciationFailureKind expected)
    {
        var assessor = Assessor(new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("internal provider detail")
        }), new StubConverter(new PronunciationWav([1], 1)));

        var error = await Assert.ThrowsAsync<PronunciationAssessmentException>(() =>
            assessor.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None));

        Assert.Equal(expected, error.Kind);
        Assert.DoesNotContain("internal", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Network_failures_and_invalid_json_are_provider_errors()
    {
        var offline = Assessor(new StubHandler(_ => throw new HttpRequestException("connection refused")),
            new StubConverter(new PronunciationWav([1], 1)));
        var garbage = Assessor(new StubHandler(_ => Json("not json")),
            new StubConverter(new PronunciationWav([1], 1)));

        Assert.Equal(PronunciationFailureKind.ProviderError, (await Assert.ThrowsAsync<PronunciationAssessmentException>(
            () => offline.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None))).Kind);
        Assert.Equal(PronunciationFailureKind.ProviderError, (await Assert.ThrowsAsync<PronunciationAssessmentException>(
            () => garbage.AssessAsync([9], "audio/webm", "think", "en-US", CancellationToken.None))).Kind);
    }

    private static AzurePronunciationAssessor Assessor(StubHandler handler, StubConverter converter) =>
        new(new HttpClient(handler), Options.Create(Azure), converter);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubConverter(PronunciationWav wav) : IPronunciationAudioConverter
    {
        public string? ContentType { get; private set; }

        public Task<PronunciationWav> ToWavAsync(byte[] audio, string contentType,
            CancellationToken cancellationToken)
        {
            ContentType = contentType;
            return Task.FromResult(wav);
        }
    }

    private sealed record RecordedRequest(Uri Uri, Dictionary<string, string> Headers, byte[] Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value));
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.RequestUri!, headers, body));
            return respond(request);
        }
    }
}
