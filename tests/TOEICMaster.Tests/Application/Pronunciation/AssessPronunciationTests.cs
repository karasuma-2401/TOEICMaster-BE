using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Tests.Application.Pronunciation;

public sealed class AssessPronunciationTests
{
    private static readonly byte[] WebM = [0x1a, 0x45, 0xdf, 0xa3, 1, 2, 3];

    [Theory]
    [InlineData("think", true)]
    [InlineData("  think  ", true)]
    [InlineData("o'clock", true)]
    [InlineData("check-in desk", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("think1", false)]
    [InlineData("th\u00ednk", false)]
    [InlineData("<script>", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void Reference_text_allows_only_short_english_words(string value, bool expected) =>
        Assert.Equal(expected, PronunciationRules.IsReferenceText(value));

    [Fact]
    public void Validator_rejects_empty_or_oversized_audio_and_unknown_locales()
    {
        var validator = new AssessPronunciationCommandValidator();

        Assert.True(validator.Validate(new AssessPronunciationCommand(WebM, "think", null)).IsValid);
        Assert.True(validator.Validate(new AssessPronunciationCommand(WebM, "think", "en-GB")).IsValid);
        Assert.False(validator.Validate(new AssessPronunciationCommand([], "think", null)).IsValid);
        Assert.False(validator.Validate(new AssessPronunciationCommand(
            new byte[PronunciationRules.MaxAudioBytes + 1], "think", null)).IsValid);
        Assert.False(validator.Validate(new AssessPronunciationCommand(WebM, "think", "vi-VN")).IsValid);
        Assert.False(validator.Validate(new AssessPronunciationCommand(WebM, "th1nk", null)).IsValid);
    }

    [Theory]
    [InlineData(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }, "audio/webm")]
    [InlineData(new byte[] { (byte)'O', (byte)'g', (byte)'g', (byte)'S' }, "audio/ogg")]
    [InlineData(new byte[] { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0 }, "audio/mp4")]
    [InlineData(new byte[]
    {
        (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'A', (byte)'V', (byte)'E'
    }, "audio/wav")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4e, 0x47 }, null)]
    public void Audio_is_detected_from_the_file_signature(byte[] bytes, string? expected) =>
        Assert.Equal(expected, PronunciationRules.DetectAudio(bytes));

    [Fact]
    public async Task Handler_passes_trimmed_text_detected_type_default_locale_and_token()
    {
        var assessor = new RecordingAssessor();
        using var cancellation = new CancellationTokenSource();
        var handler = new AssessPronunciationCommandHandler(assessor);

        var result = await handler.Handle(new AssessPronunciationCommand(WebM, " think ", null), cancellation.Token);

        Assert.Same(RecordingAssessor.Result, result);
        Assert.Equal(("audio/webm", "think", "en-US"), (assessor.ContentType, assessor.ReferenceText, assessor.Locale));
        Assert.Equal(cancellation.Token, assessor.Token);
    }

    [Fact]
    public async Task Handler_rejects_unknown_audio_without_calling_the_assessor()
    {
        var assessor = new RecordingAssessor();
        var handler = new AssessPronunciationCommandHandler(assessor);

        var error = await Assert.ThrowsAsync<PronunciationAssessmentException>(() =>
            handler.Handle(new AssessPronunciationCommand([1, 2, 3, 4], "think", null), CancellationToken.None));

        Assert.Equal(PronunciationFailureKind.InvalidAudio, error.Kind);
        Assert.Equal(0, assessor.Calls);
    }

    private sealed class RecordingAssessor : IPronunciationAssessor
    {
        public static readonly PronunciationAssessmentDto Result = PronunciationAssessmentDto.NoSpeech("think");
        public int Calls { get; private set; }
        public string? ContentType { get; private set; }
        public string? ReferenceText { get; private set; }
        public string? Locale { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<PronunciationAssessmentDto> AssessAsync(byte[] audio, string contentType, string referenceText,
            string locale, CancellationToken cancellationToken)
        {
            Calls++;
            (ContentType, ReferenceText, Locale, Token) = (contentType, referenceText, locale, cancellationToken);
            return Task.FromResult(Result);
        }
    }
}
