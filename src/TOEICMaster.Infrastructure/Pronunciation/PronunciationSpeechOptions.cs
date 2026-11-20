using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Pronunciation;

/// <summary>
/// Settings for the IPA pronunciation assessor. Bound read-only from the existing <c>AzureSpeech</c> section so
/// one Azure Speech resource serves both features without sharing code.
/// </summary>
public sealed class PronunciationSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    [Range(5, 120)]
    public int RequestTimeoutSeconds { get; set; } = 30;
    [Required]
    public string FfmpegPath { get; set; } = "ffmpeg";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Key) &&
                                (!string.IsNullOrWhiteSpace(Region) ||
                                 Uri.TryCreate(Endpoint, UriKind.Absolute, out _));

    public Uri SpeechToTextBaseUri() => new(string.IsNullOrWhiteSpace(Endpoint)
        ? $"https://{Region.Trim()}.stt.speech.microsoft.com/"
        : Endpoint.TrimEnd('/') + "/");
}
