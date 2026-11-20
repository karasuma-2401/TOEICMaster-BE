namespace TOEICMaster.Application.Pronunciation;

// IPA pronunciation practice (learner side). A self-contained feature: it does not share code or data with
// Speaking. Recordings are scored in memory and never stored.

public sealed record PronunciationPhonemeCandidateDto(string Phoneme, double Score);

public sealed record PronunciationPhonemeDto(string Phoneme, double AccuracyScore,
    IReadOnlyList<PronunciationPhonemeCandidateDto> Candidates);

public sealed record PronunciationWordDto(string Word, double AccuracyScore, string ErrorType,
    IReadOnlyList<PronunciationPhonemeDto> Phonemes);

public static class PronunciationRecognitionStatus
{
    public const string Recognized = "Recognized";
    public const string NoSpeech = "NoSpeech";
}

public sealed record PronunciationAssessmentDto(string RecognitionStatus, string ReferenceText,
    string RecognizedText, double AccuracyScore, double PronunciationScore, double CompletenessScore,
    IReadOnlyList<PronunciationWordDto> Words)
{
    public static PronunciationAssessmentDto NoSpeech(string referenceText) => new(
        PronunciationRecognitionStatus.NoSpeech, referenceText, string.Empty, 0, 0, 0, []);
}

/// <summary>Scores one short recording of <c>referenceText</c> at phoneme level.</summary>
public interface IPronunciationAssessor
{
    Task<PronunciationAssessmentDto> AssessAsync(byte[] audio, string contentType, string referenceText,
        string locale, CancellationToken cancellationToken);
}

public enum PronunciationFailureKind
{
    InvalidAudio,
    Rejected,
    NotConfigured,
    ProviderError,
    Timeout
}

/// <summary>A failure whose <see cref="Exception.Message"/> is safe to show to the learner.</summary>
public sealed class PronunciationAssessmentException(PronunciationFailureKind kind, string code, string safeMessage,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public PronunciationFailureKind Kind { get; } = kind;
    public string Code { get; } = code;

    public static PronunciationAssessmentException InvalidAudio(string message =
        "Bản thu không hợp lệ. Hãy thu lại trong tối đa 10 giây.") =>
        new(PronunciationFailureKind.InvalidAudio, "INVALID_AUDIO", message);

    public static PronunciationAssessmentException NotConfigured() =>
        new(PronunciationFailureKind.NotConfigured, "NOT_CONFIGURED", "Chưa chấm được phát âm lúc này.");

    public static PronunciationAssessmentException ProviderError(Exception? innerException = null) =>
        new(PronunciationFailureKind.ProviderError, "SPEECH_PROVIDER_ERROR", "Chưa chấm được phát âm lúc này.",
            innerException);

    public static PronunciationAssessmentException Timeout(Exception? innerException = null) =>
        new(PronunciationFailureKind.Timeout, "SPEECH_TIMEOUT", "Hệ thống chấm phản hồi quá lâu.", innerException);

    public static PronunciationAssessmentException Rejected() =>
        new(PronunciationFailureKind.Rejected, "SPEECH_REJECTED", "Hệ thống chấm không xử lý được bản thu này.");
}
