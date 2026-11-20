using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Pronunciation;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Pronunciation;

public sealed class PronunciationApiFactory : ApiFactory
{
    public FakePronunciationAssessor Assessor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IPronunciationAssessor>(Assessor));
    }
}

public sealed class FakePronunciationAssessor : IPronunciationAssessor
{
    public Exception? Failure { get; set; }
    public (string ContentType, string ReferenceText, string Locale)? LastCall { get; private set; }

    public Task<PronunciationAssessmentDto> AssessAsync(byte[] audio, string contentType, string referenceText,
        string locale, CancellationToken cancellationToken)
    {
        LastCall = (contentType, referenceText, locale);
        if (Failure is not null)
            throw Failure;
        return Task.FromResult(new PronunciationAssessmentDto(PronunciationRecognitionStatus.Recognized,
            referenceText, "think", 78, 80.5, 100,
        [
            new PronunciationWordDto("think", 78, "None",
            [
                new PronunciationPhonemeDto("θ", 54, [new PronunciationPhonemeCandidateDto("s", 71)]),
                new PronunciationPhonemeDto("ɪ", 92, [])
            ])
        ]));
    }
}

public sealed class PronunciationApiTests : IDisposable
{
    private const string Endpoint = "/api/v1/pronunciation/assessments";
    private static readonly byte[] WebM = [0x1a, 0x45, 0xdf, 0xa3, 1, 2, 3, 4];
    private readonly PronunciationApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static User CreateLearner() => User.CreateExternal("Learner", "learner@example.com",
        "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);

    private AuthenticationHeaderValue IssueBearerToken(User user) => new("Bearer",
        _factory.Services.GetRequiredService<IAuthenticationTokenService>()
            .IssueForLogin(user, DateTimeOffset.UtcNow, false).AccessToken);

    private HttpClient SignedIn(User? user = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = IssueBearerToken(user ?? CreateLearner());
        return client;
    }

    private static MultipartFormDataContent Form(byte[]? audio, string? referenceText, string? locale = null)
    {
        var form = new MultipartFormDataContent();
        if (audio is not null)
        {
            var file = new ByteArrayContent(audio);
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
            form.Add(file, "file", "recording.webm");
        }
        if (referenceText is not null)
            form.Add(new StringContent(referenceText), "referenceText");
        if (locale is not null)
            form.Add(new StringContent(locale), "locale");
        return form;
    }

    [Fact]
    public async Task Requires_sign_in()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(Endpoint, Form(WebM, "think"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(_factory.Assessor.LastCall);
    }

    [Fact]
    public async Task Rate_limit_on_the_31st_request_is_shared_by_tokens_for_the_same_user()
    {
        var learner = CreateLearner();
        using var client = SignedIn(learner);
        for (var requestNumber = 1; requestNumber <= 30; requestNumber++)
        {
            using var form = Form(WebM, "think");
            using var response = await client.PostAsync(Endpoint, form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = IssueBearerToken(learner);
        using var limitedForm = Form(WebM, "think");
        using var limited = await client.PostAsync(Endpoint, limitedForm);
        AssertRateLimited(limited);

        using var otherClient = SignedIn();
        using var otherForm = Form(WebM, "think");
        using var otherResponse = await otherClient.PostAsync(Endpoint, otherForm);
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
    }

    [Fact]
    public async Task Missing_and_invalid_credentials_share_the_client_ip_quota()
    {
        _factory.UseKestrel(0);
        using var client = _factory.CreateClient();
        for (var requestNumber = 1; requestNumber <= 29; requestNumber++)
        {
            using var form = Form(WebM, "think");
            using var response = await client.PostAsync(Endpoint, form);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var unauthorizedForm = Form(WebM, "think");
        using var unauthorized = await client.PostAsync(Endpoint, unauthorizedForm);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "another-invalid-token");
        using var limitedForm = Form(WebM, "think");
        using var limited = await client.PostAsync(Endpoint, limitedForm);
        AssertRateLimited(limited);

        client.DefaultRequestHeaders.Authorization = null;
        using var anonymousForm = Form(WebM, "think");
        using var anonymous = await client.PostAsync(Endpoint, anonymousForm);
        AssertRateLimited(anonymous);
        Assert.Null(_factory.Assessor.LastCall);
    }

    private static void AssertRateLimited(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Returns_the_frontend_contract()
    {
        using var client = SignedIn();

        using var response = await client.PostAsync(Endpoint, Form(WebM, "think", "en-US"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("audio/webm", "think", "en-US"), _factory.Assessor.LastCall);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("Recognized", root.GetProperty("recognitionStatus").GetString());
        Assert.Equal("think", root.GetProperty("referenceText").GetString());
        Assert.Equal("think", root.GetProperty("recognizedText").GetString());
        Assert.Equal(78, root.GetProperty("accuracyScore").GetDouble());
        Assert.Equal(80.5, root.GetProperty("pronunciationScore").GetDouble());
        Assert.Equal(100, root.GetProperty("completenessScore").GetDouble());
        var word = root.GetProperty("words")[0];
        Assert.Equal("None", word.GetProperty("errorType").GetString());
        var phoneme = word.GetProperty("phonemes")[0];
        Assert.Equal("θ", phoneme.GetProperty("phoneme").GetString());
        Assert.Equal(54, phoneme.GetProperty("accuracyScore").GetDouble());
        Assert.Equal("s", phoneme.GetProperty("candidates")[0].GetProperty("phoneme").GetString());
        Assert.Equal(71, phoneme.GetProperty("candidates")[0].GetProperty("score").GetDouble());
    }

    [Theory]
    [InlineData(false, "think", null)]
    [InlineData(true, null, null)]
    [InlineData(true, "th1nk", null)]
    [InlineData(true, "think", "vi-VN")]
    public async Task Invalid_input_is_400(bool withAudio, string? referenceText, string? locale)
    {
        using var client = SignedIn();

        using var response = await client.PostAsync(Endpoint, Form(withAudio ? WebM : null, referenceText, locale));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(_factory.Assessor.LastCall);
    }

    [Fact]
    public async Task Unknown_audio_format_is_400_with_a_code()
    {
        using var client = SignedIn();

        using var response = await client.PostAsync(Endpoint, Form([1, 2, 3, 4, 5], "think"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_AUDIO", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Oversized_audio_is_413()
    {
        using var client = SignedIn();
        var audio = new byte[PronunciationRules.MaxAudioBytes + 1];
        WebM.CopyTo(audio, 0);

        using var response = await client.PostAsync(Endpoint, Form(audio, "think"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData(PronunciationFailureKind.Rejected, HttpStatusCode.UnprocessableEntity, "SPEECH_REJECTED")]
    [InlineData(PronunciationFailureKind.NotConfigured, HttpStatusCode.ServiceUnavailable, "NOT_CONFIGURED")]
    [InlineData(PronunciationFailureKind.ProviderError, HttpStatusCode.BadGateway, "SPEECH_PROVIDER_ERROR")]
    [InlineData(PronunciationFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "SPEECH_TIMEOUT")]
    public async Task Assessor_failures_map_to_problem_details(PronunciationFailureKind kind,
        HttpStatusCode expected, string code)
    {
        _factory.Assessor.Failure = kind switch
        {
            PronunciationFailureKind.Rejected => PronunciationAssessmentException.Rejected(),
            PronunciationFailureKind.NotConfigured => PronunciationAssessmentException.NotConfigured(),
            PronunciationFailureKind.Timeout => PronunciationAssessmentException.Timeout(),
            _ => PronunciationAssessmentException.ProviderError(new InvalidOperationException("secret detail"))
        };
        using var client = SignedIn();

        using var response = await client.PostAsync(Endpoint, Form(WebM, "think"));

        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret detail", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
}
