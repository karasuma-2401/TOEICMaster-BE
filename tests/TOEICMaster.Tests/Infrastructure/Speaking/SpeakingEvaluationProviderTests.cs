using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Speaking.Evaluations;
using TOEICMaster.Infrastructure.Writing.Evaluations;

namespace TOEICMaster.Tests.Infrastructure.Speaking;

public sealed class SpeakingEvaluationProviderTests
{
    private static readonly AzureSpeechOptions Azure = new()
    {
        Enabled = true,
        Key = "speech-key",
        Region = "southeastasia"
    };

    [Fact]
    public async Task Azure_requests_unscripted_pronunciation_assessment_on_converted_wav()
    {
        var handler = new StubHandler(_ => Json(Recognized("Hello there.", accuracy: 90, fluency: 80, prosody: 70,
            pronunciation: 85, nested: false)));
        var assessor = Assessor(handler, new StubConverter(new SpeakingWavChunk([1, 2, 3], 4)));

        var result = await assessor.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://southeastasia.stt.speech.microsoft.com/speech/recognition/conversation/" +
                     "cognitiveservices/v1?language=en-US&format=detailed", request.Uri.ToString());
        Assert.Equal("speech-key", request.Headers["Ocp-Apim-Subscription-Key"]);
        Assert.Equal("audio/wav; codecs=audio/pcm; samplerate=16000", request.Headers["Content-Type"]);
        Assert.Equal([1, 2, 3], request.Body);
        using var assessment = JsonDocument.Parse(Encoding.UTF8.GetString(
            Convert.FromBase64String(request.Headers["Pronunciation-Assessment"])));
        Assert.Equal("Comprehensive", assessment.RootElement.GetProperty("Dimension").GetString());
        Assert.True(assessment.RootElement.GetProperty("EnableProsodyAssessment").GetBoolean());
        Assert.False(assessment.RootElement.TryGetProperty("ReferenceText", out _));
        Assert.Equal((SpeakingRecognitionStatus.Recognized, "Hello there.", "hello there", 90d, 80d, 70d, 85d, 4d),
            (result.Status, result.Transcript, result.LexicalTranscript, result.AccuracyScore, result.FluencyScore,
                result.ProsodyScore, result.PronunciationScore, result.AudioSeconds));
        Assert.Equal([new SpeakingWordIssue("there", 40)], result.WordIssues);
    }

    [Fact]
    public async Task Azure_chunk_scores_are_weighted_by_duration_and_nested_scores_are_read()
    {
        var responses = new Queue<string>([
            Recognized("First part.", 100, 100, 100, 100, nested: true),
            Recognized("Second part.", 40, 40, null, 40, nested: true)
        ]);
        var assessor = Assessor(new StubHandler(_ => Json(responses.Dequeue())),
            new StubConverter(new SpeakingWavChunk([1], 25), new SpeakingWavChunk([2], 5)));

        var result = await assessor.AssessAsync([9], "audio/ogg", "en-US", CancellationToken.None);

        Assert.Equal("First part. Second part.", result.Transcript);
        Assert.Equal(90, result.PronunciationScore, 6);
        Assert.Equal(100d, result.ProsodyScore);
        Assert.Equal(30, result.AudioSeconds);
    }

    [Fact]
    public async Task Azure_falls_back_to_assessing_against_its_own_transcript_when_unscripted_is_rejected()
    {
        var handler = new StubHandler(request => request.Headers.TryGetValue("Pronunciation-Assessment", out var header)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(header)).Contains("ReferenceText")
                ? Json(Recognized("Hello there.", 70, 70, 70, 70, nested: false))
                : new HttpResponseMessage(HttpStatusCode.BadRequest)
            : Json("""{"RecognitionStatus":"Success","DisplayText":"Hello there.","NBest":[{"Lexical":"hello there","Display":"Hello there."}]}"""));
        var assessor = Assessor(handler, new StubConverter(new SpeakingWavChunk([1], 3)));

        var result = await assessor.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        using var reference = JsonDocument.Parse(Encoding.UTF8.GetString(
            Convert.FromBase64String(handler.Requests[2].Headers["Pronunciation-Assessment"])));
        Assert.Equal("hello there", reference.RootElement.GetProperty("ReferenceText").GetString());
        Assert.Equal(70, result.PronunciationScore);
    }

    [Fact]
    public async Task Silence_and_undecodable_audio_are_reported_as_no_speech()
    {
        var silent = Assessor(new StubHandler(_ => Json("""{"RecognitionStatus":"InitialSilenceTimeout"}""")),
            new StubConverter(new SpeakingWavChunk([1], 10)));
        Assert.Equal(SpeakingRecognitionStatus.NoSpeech,
            (await silent.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None)).Status);

        var handler = new StubHandler(_ => throw new InvalidOperationException("no request expected"));
        var empty = Assessor(handler, new StubConverter());
        var result = await empty.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None);
        Assert.Equal((SpeakingRecognitionStatus.NoSpeech, 0d), (result.Status, result.AudioSeconds));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "SPEECH_AUTHENTICATION", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "SPEECH_QUOTA", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "SPEECH_PROVIDER_ERROR", true)]
    public async Task Azure_errors_are_classified_for_retries(HttpStatusCode status, string code, bool transient)
    {
        var assessor = Assessor(new StubHandler(_ => new HttpResponseMessage(status)),
            new StubConverter(new SpeakingWavChunk([1], 3)));

        var error = await Assert.ThrowsAsync<SpeakingEvaluatorException>(() =>
            assessor.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None));

        Assert.Equal((code, transient), (error.FailureCode, error.IsTransient));
    }

    [Fact]
    public async Task A_recognition_without_pronunciation_scores_is_a_configuration_error()
    {
        var assessor = Assessor(new StubHandler(_ => Json(
                """{"RecognitionStatus":"Success","NBest":[{"Lexical":"hi","Display":"Hi."}]}""")),
            new StubConverter(new SpeakingWavChunk([1], 3)));

        var error = await Assert.ThrowsAsync<SpeakingEvaluatorException>(() =>
            assessor.AssessAsync([9], "audio/webm", "en-US", CancellationToken.None));

        Assert.Equal(("PRONUNCIATION_UNAVAILABLE", false), (error.FailureCode, error.IsTransient));
    }

    [Fact]
    public async Task A_missing_ffmpeg_fails_the_job_instead_of_retrying()
    {
        var converter = new FfmpegSpeakingAudioConverter(Options.Create(new AzureSpeechOptions
        {
            FfmpegPath = "/nonexistent/toeicmaster-ffmpeg"
        }));

        var error = await Assert.ThrowsAsync<SpeakingEvaluatorException>(() =>
            converter.ToWavChunksAsync([1, 2, 3], "audio/webm", CancellationToken.None));

        Assert.Equal(("AUDIO_CONVERTER_UNAVAILABLE", false), (error.FailureCode, error.IsTransient));
    }

    [Fact]
    public void Wav_duration_comes_from_the_byte_rate_and_data_chunk()
    {
        var wav = new byte[44 + 64_000];
        "RIFF"u8.CopyTo(wav);
        "WAVEfmt "u8.CopyTo(wav.AsSpan(8));
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes(32_000).CopyTo(wav, 28);
        "data"u8.CopyTo(wav.AsSpan(36));
        BitConverter.GetBytes(64_000).CopyTo(wav, 40);

        Assert.Equal(2, FfmpegSpeakingAudioConverter.WavSeconds(wav));
        Assert.Equal(0, FfmpegSpeakingAudioConverter.WavSeconds("not a wav"u8));
    }

    [Fact]
    public async Task Gemini_grades_transcripts_with_the_header_key_and_a_json_schema()
    {
        var handler = new StubHandler(_ => Json(JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = """
                                    {"summary":"Khá tốt.","strengths":["Rõ ràng."],"improvements":[],
                                     "items":[{"position":2,"grammarScore":75,"vocabularyScore":60,
                                               "relevanceScore":90,"feedback":"Ổn.","suggestedResponse":"Better."}]}
                                    """
                            }
                        }
                    }
                }
            }
        })));
        var evaluator = new GeminiSpeakingLanguageEvaluator(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            Options.Create(new GeminiOptions { Enabled = true, ApiKey = "gemini-key" }));

        var result = await evaluator.EvaluateAsync(new SpeakingLanguageWork(Guid.NewGuid(), "gemini-test",
            "toeic-speaking-v1", [new SpeakingLanguageItem(2, 3, 1, """{"part":3,"questions":["a","b","c"]}""",
                """{"rubricNotes":"r","modelAnswer":"m"}""", "I read every day.", 80, 75, 70, null, [])]),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent",
            request.Uri.ToString());
        Assert.Equal("gemini-key", request.Headers["x-goog-api-key"]);
        Assert.DoesNotContain("gemini-key", request.Uri.ToString());
        var body = Encoding.UTF8.GetString(request.Body);
        Assert.Contains("I read every day.", body);
        Assert.Contains("responseJsonSchema", body);
        Assert.Equal(("Khá tốt.", 75, 60, 90), (result.Summary, result.Items[0].GrammarScore,
            result.Items[0].VocabularyScore, result.Items[0].RelevanceScore));
    }

    [Fact]
    public async Task Gemini_quota_errors_are_retried_and_a_missing_key_is_not()
    {
        var work = new SpeakingLanguageWork(Guid.NewGuid(), "gemini-test", "v1", []);
        var busy = new GeminiSpeakingLanguageEvaluator(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)))
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            }, Options.Create(new GeminiOptions { Enabled = true, ApiKey = "k" }));
        var quota = await Assert.ThrowsAsync<SpeakingEvaluatorException>(() =>
            busy.EvaluateAsync(work, CancellationToken.None));
        Assert.Equal(("QUOTA", true), (quota.FailureCode, quota.IsTransient));

        var unconfigured = new GeminiSpeakingLanguageEvaluator(new HttpClient(),
            Options.Create(new GeminiOptions { Enabled = false }));
        var missing = await Assert.ThrowsAsync<SpeakingEvaluatorException>(() =>
            unconfigured.EvaluateAsync(work, CancellationToken.None));
        Assert.Equal(("NOT_CONFIGURED", false), (missing.FailureCode, missing.IsTransient));
    }

    private static AzureSpeakingSpeechAssessor Assessor(StubHandler handler, ISpeakingAudioConverter converter) =>
        new(new HttpClient(handler), Options.Create(Azure), converter);

    private static string Recognized(string display, double accuracy, double fluency, double? prosody,
        double pronunciation, bool nested)
    {
        var scores = new Dictionary<string, object?>
        {
            ["AccuracyScore"] = accuracy,
            ["FluencyScore"] = fluency,
            ["PronScore"] = pronunciation
        };
        if (prosody is not null)
            scores["ProsodyScore"] = prosody;
        var words = new object[]
        {
            new { Word = "hello", AccuracyScore = 95, ErrorType = "None" },
            new { Word = "there", AccuracyScore = 40, ErrorType = "Mispronunciation" }
        };
        var best = new Dictionary<string, object?>
        {
            ["Lexical"] = display.ToLowerInvariant().Trim('.'),
            ["Display"] = display,
            ["Words"] = words
        };
        if (nested)
            best["PronunciationAssessment"] = scores;
        else
            foreach (var (key, value) in scores)
                best[key] = value;
        return JsonSerializer.Serialize(new { RecognitionStatus = "Success", DisplayText = display, NBest = new[] { best } });
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubConverter(params SpeakingWavChunk[] chunks) : ISpeakingAudioConverter
    {
        public Task<IReadOnlyList<SpeakingWavChunk>> ToWavChunksAsync(byte[] audio, string contentType,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SpeakingWavChunk>>(chunks);
    }

    private sealed record CapturedRequest(Uri Uri, Dictionary<string, string> Headers, byte[] Body);

    private sealed class StubHandler(Func<CapturedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = request.Headers.Concat(request.Content?.Headers ??
                                                 Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var captured = new CapturedRequest(request.RequestUri!, headers, body);
            Requests.Add(captured);
            return respond(captured);
        }
    }
}
