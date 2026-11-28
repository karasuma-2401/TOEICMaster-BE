using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Tests.Application.ExerciseBank;

public sealed class LrExerciseMediaValidationTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.test/audio.mp3")]
    [InlineData("/\\evil.test/audio.mp3")]
    [InlineData("https://example.test/audio.mp3\n")]
    [InlineData("ftp://example.test/audio.mp3")]
    [InlineData("https://")]
    public void Media_validators_reject_unsafe_or_invalid_urls(string url)
    {
        var patch = new UpdateLrExerciseItemMediaCommand(Guid.NewGuid(), 1, Guid.NewGuid(),
            new LrExerciseItemMediaInput(1, "DRAFT", url, url));
        var create = new LrExerciseItemInput("P1", "Topic", "EASY", "DRAFT", null, url, url,
            [new LrExerciseQuestionInput("Q1", "Prompt", "A", "B", "C", "D", "A", null, null)]);

        Assert.False(new UpdateLrExerciseItemMediaCommandValidator().Validate(patch).IsValid);
        Assert.False(new LrExerciseItemInputValidator().Validate(create).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lr-exercise/audio/example.mp3")]
    [InlineData("/api/v1/media/example")]
    [InlineData("https://example.test/audio.mp3")]
    [InlineData("http://example.test/audio.mp3")]
    public void Media_validators_accept_supported_values(string? url)
    {
        var patch = new UpdateLrExerciseItemMediaCommand(Guid.NewGuid(), 1, Guid.NewGuid(),
            new LrExerciseItemMediaInput(1, "DRAFT", url, url));
        Assert.True(new UpdateLrExerciseItemMediaCommandValidator().Validate(patch).IsValid);
    }
}
