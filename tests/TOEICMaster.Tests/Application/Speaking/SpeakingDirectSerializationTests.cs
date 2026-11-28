using System.Text.Json;
using System.Text.Json.Serialization;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingDirectSerializationTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Direct_part4_content_omits_null_caller_intro_and_unrelated_part_fields()
    {
        var dto = new SpeakingTestQuestionContentDto(
            RubricNotes: "Check clarity",
            ModelAnswer: "The meeting starts at 9 AM.",
            DocumentTitle: "Conference Schedule",
            DocumentRows: [new SpeakingInfoRowDto("Date", "May 12")],
            CallerIntro: null,
            Questions: ["When does the conference start?", "Where is it held?", "Who is speaking?"]);

        var json = JsonSerializer.Serialize(dto, WebJsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("callerIntro", out _));
        Assert.False(root.TryGetProperty("passage", out _));
        Assert.False(root.TryGetProperty("prompt", out _));
        Assert.Equal("Conference Schedule", root.GetProperty("documentTitle").GetString());
        Assert.Equal(3, root.GetProperty("questions").GetArrayLength());
    }

    [Fact]
    public void Direct_part4_content_preserves_non_null_caller_intro()
    {
        var dto = new SpeakingTestQuestionContentDto(
            RubricNotes: "Check clarity",
            ModelAnswer: "The meeting starts at 9 AM.",
            DocumentTitle: "Conference Schedule",
            DocumentRows: [new SpeakingInfoRowDto("Date", "May 12")],
            CallerIntro: "Hello, I am calling about the conference.",
            Questions: ["When does it start?", "Where is it held?", "Who is speaking?"]);

        var json = JsonSerializer.Serialize(dto, WebJsonOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Hello, I am calling about the conference.",
            document.RootElement.GetProperty("callerIntro").GetString());
    }
}
