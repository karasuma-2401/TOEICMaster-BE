using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Speaking.Evaluations;

public sealed class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    public bool Enabled { get; init; }
    public string Key { get; init; } = string.Empty;
    /// <summary>Region of the Speech resource, for example <c>southeastasia</c>.</summary>
    public string Region { get; init; } = string.Empty;
    /// <summary>Optional full speech-to-text host; defaults to https://{Region}.stt.speech.microsoft.com.</summary>
    public string? Endpoint { get; init; }
    /// <summary>Prosody (stress, intonation, rhythm) is only scored for en-US.</summary>
    [Required]
    public string Locale { get; init; } = "en-US";
    [Range(5, 120)]
    public int RequestTimeoutSeconds { get; init; } = 30;
    [Required]
    public string FfmpegPath { get; init; } = "ffmpeg";
    /// <summary>The REST API scores pronunciation for at most 30 seconds of audio per request.</summary>
    [Range(5, 30)]
    public int ChunkSeconds { get; init; } = 25;

    public Uri SpeechToTextBaseUri() => new(string.IsNullOrWhiteSpace(Endpoint)
        ? $"https://{Region.Trim()}.stt.speech.microsoft.com/"
        : Endpoint.TrimEnd('/') + "/");
}
