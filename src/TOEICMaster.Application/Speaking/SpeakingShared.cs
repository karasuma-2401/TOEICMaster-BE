using System.Text.Json;
using System.Text.Json.Serialization;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking;

public sealed class SpeakingNotFoundException(string message = "Không tìm thấy dữ liệu Speaking.") : Exception(message);

/// <summary>409: stale version, duplicate code, locked resource or closed recording window.</summary>
public sealed class SpeakingConflictException(string message) : Exception(message);

/// <summary>422: a test does not meet the publishing requirements; one entry per failed requirement.</summary>
public sealed class SpeakingPublishRequirementsException(IReadOnlyDictionary<string, string[]> errors) : Exception(
    "Đề Speaking chưa đủ điều kiện để công bố.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>The row changed in another request (for example two tabs pressing "next" at once).</summary>
public sealed class SpeakingConcurrencyException() : Exception(
    "Lượt làm đã được cập nhật ở một phiên khác. Hãy tải lại trang.");

public sealed class InvalidSpeakingMediaException(string message) : Exception(message);

public sealed class SpeakingMediaTooLargeException(string message) : Exception(message);

public sealed record SpeakingMediaContent(byte[] Data, string ContentType);

public interface ISpeakingMediaStore
{
    Task UploadAsync(SpeakingMediaKind kind, string objectKey, byte[] content, string contentType,
        CancellationToken cancellationToken);
    Task<byte[]> DownloadAsync(SpeakingMediaKind kind, string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(SpeakingMediaKind kind, string objectKey, CancellationToken cancellationToken);
}

public sealed record SpeakingPageDto<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record SpeakingInfoRowDto(string Label, string Value);

/// <summary>Part-specific content shared by exercise storage, admin previews and attempt questions.</summary>
public sealed record SpeakingContentDto(
    int Part,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Passage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ImageAssetId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ImageAlt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string?>? Questions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DocumentTitle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SpeakingInfoRowDto>? DocumentRows = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CallerIntro = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Prompt = null);

public static class SpeakingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Invalid Speaking JSON for {typeof(T).Name}.");
}

/// <summary>Upper-case API/database codes for Speaking enums.</summary>
public static class SpeakingCodes
{
    public static string ToCode(this SpeakingDifficulty value) => value switch
    {
        SpeakingDifficulty.Easy => "EASY",
        SpeakingDifficulty.Medium => "MEDIUM",
        SpeakingDifficulty.Hard => "HARD",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingExerciseStatus value) => value switch
    {
        SpeakingExerciseStatus.Draft => "DRAFT",
        SpeakingExerciseStatus.Published => "PUBLISHED",
        SpeakingExerciseStatus.Archived => "ARCHIVED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingTestStatus value) => value switch
    {
        SpeakingTestStatus.Draft => "DRAFT",
        SpeakingTestStatus.Published => "PUBLISHED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingMediaKind value) => value switch
    {
        SpeakingMediaKind.Image => "IMAGE",
        SpeakingMediaKind.Recording => "RECORDING",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingAttemptMode value) => value switch
    {
        SpeakingAttemptMode.Full => "FULL",
        SpeakingAttemptMode.Part1 => "PART_1",
        SpeakingAttemptMode.Part2 => "PART_2",
        SpeakingAttemptMode.Part3 => "PART_3",
        SpeakingAttemptMode.Part4 => "PART_4",
        SpeakingAttemptMode.Part5 => "PART_5",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingAttemptStatus value) => value switch
    {
        SpeakingAttemptStatus.InProgress => "IN_PROGRESS",
        SpeakingAttemptStatus.Submitted => "SUBMITTED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingPhase value) => value switch
    {
        SpeakingPhase.Reading => "READING",
        SpeakingPhase.Preparing => "PREPARING",
        SpeakingPhase.Responding => "RESPONDING",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingEvaluationStatus value) => value switch
    {
        SpeakingEvaluationStatus.Queued => "QUEUED",
        SpeakingEvaluationStatus.Processing => "PROCESSING",
        SpeakingEvaluationStatus.Retrying => "RETRYING",
        SpeakingEvaluationStatus.Completed => "COMPLETED",
        SpeakingEvaluationStatus.Failed => "FAILED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToCode(this SpeakingRecognitionStatus value) => value switch
    {
        SpeakingRecognitionStatus.Recognized => "RECOGNIZED",
        SpeakingRecognitionStatus.NoSpeech => "NO_SPEECH",
        SpeakingRecognitionStatus.NoRecording => "NO_RECORDING",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static bool TryParseDifficulty(string? value, out SpeakingDifficulty result) =>
        TryParse(value, out result, SpeakingDifficulty.Easy, SpeakingDifficulty.Medium, SpeakingDifficulty.Hard);

    public static bool TryParseExerciseStatus(string? value, out SpeakingExerciseStatus result) =>
        TryParse(value, out result, SpeakingExerciseStatus.Draft, SpeakingExerciseStatus.Published,
            SpeakingExerciseStatus.Archived);

    public static bool TryParseTestStatus(string? value, out SpeakingTestStatus result) =>
        TryParse(value, out result, SpeakingTestStatus.Draft, SpeakingTestStatus.Published);

    public static bool TryParseMode(string? value, out SpeakingAttemptMode result) =>
        TryParse(value, out result, SpeakingAttemptMode.Full, SpeakingAttemptMode.Part1, SpeakingAttemptMode.Part2,
            SpeakingAttemptMode.Part3, SpeakingAttemptMode.Part4, SpeakingAttemptMode.Part5);

    public static T Parse<T>(string value) where T : struct, Enum => Enum.GetValues<T>()
        .Single(candidate => Code(candidate) == value);

    /// <summary>The Part a mode is limited to, or null for a full test.</summary>
    public static int? Part(this SpeakingAttemptMode mode) => mode == SpeakingAttemptMode.Full ? null : (int)mode - 1;

    private static bool TryParse<T>(string? value, out T result, params T[] candidates) where T : struct, Enum
    {
        var normalized = value?.Trim().ToUpperInvariant();
        foreach (var candidate in candidates)
        {
            if (Code(candidate) != normalized)
                continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private static string Code<T>(T value) where T : struct, Enum => value switch
    {
        SpeakingDifficulty difficulty => difficulty.ToCode(),
        SpeakingExerciseStatus status => status.ToCode(),
        SpeakingTestStatus status => status.ToCode(),
        SpeakingMediaKind kind => kind.ToCode(),
        SpeakingAttemptMode mode => mode.ToCode(),
        SpeakingAttemptStatus status => status.ToCode(),
        SpeakingPhase phase => phase.ToCode(),
        SpeakingEvaluationStatus status => status.ToCode(),
        SpeakingRecognitionStatus status => status.ToCode(),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public static class SpeakingMediaRules
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxRecordingBytes = 4 * 1024 * 1024;

    /// <summary>Upload ceiling for one answer: responseSeconds × 32 KB + 256 KB, capped at 4 MiB.</summary>
    public static int MaxRecordingBytesFor(int responseSeconds) =>
        Math.Min(MaxRecordingBytes, responseSeconds * 32 * 1024 + 256 * 1024);

    /// <summary>Strips MIME parameters such as <c>;codecs=opus</c>.</summary>
    public static string BaseContentType(string? contentType) =>
        (contentType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();

    public static string? DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 45 && bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) &&
            bytes.Slice(12, 4).SequenceEqual("IHDR"u8) && bytes.Slice(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return "image/png";
        if (bytes.Length >= 4 && bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }) &&
            bytes.Slice(bytes.Length - 2).SequenceEqual(new byte[] { 0xff, 0xd9 }))
            return "image/jpeg";
        return null;
    }

    public static string? DetectAudio(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 4 && bytes.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
            return "audio/webm";
        if (bytes.Length >= 4 && bytes.StartsWith("OggS"u8))
            return "audio/ogg";
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
            return "audio/mp4";
        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "audio/webm" => "webm",
        "audio/ogg" => "ogg",
        "audio/mp4" => "m4a",
        _ => "bin"
    };
}
