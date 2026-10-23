using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Writing.Evaluations;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    [Required]
    public string Model { get; init; } = "gemini-2.5-flash-lite";
    [Range(10, 120)]
    public int RequestTimeoutSeconds { get; init; } = 45;
    [Required]
    public string RubricVersion { get; init; } = "toeic-writing-v1";
}
