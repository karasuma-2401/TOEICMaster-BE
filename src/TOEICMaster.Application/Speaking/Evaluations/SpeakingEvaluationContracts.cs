using System.Text.Json;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Evaluations;

/// <summary>Providers and rubric snapshotted onto each job, so later configuration changes do not rewrite it.</summary>
public interface ISpeakingEvaluationSettings
{
    /// <summary>Both Azure Speech and the language model have credentials.</summary>
    bool IsConfigured { get; }
    string SpeechProvider { get; }
    string SpeechLocale { get; }
    string LanguageProvider { get; }
    string LanguageModel { get; }
    string RubricVersion { get; }
}

public sealed record SpeakingRecordingReference(string ObjectKey, string ContentType);

public sealed record SpeakingEvaluationQuestion(int Position, int Part, int QuestionIndex, string ContentJson,
    string ReviewJson, SpeakingRecordingReference? Recording);

public sealed record SpeakingEvaluationWork(Guid EvaluationId, string SpeechLocale, string LanguageModel,
    string RubricVersion, IReadOnlyList<SpeakingEvaluationQuestion> Questions);

public sealed record SpeakingWordIssue(string Word, double AccuracyScore);

/// <summary>
/// Azure Speech result for one recording. Scores are 0-100; <see cref="LexicalTranscript"/> is the spoken form
/// (numbers as words) used to compare against a Part 1 passage.
/// </summary>
public sealed record SpeakingSpeechResult(SpeakingRecognitionStatus Status, string Transcript,
    string LexicalTranscript, double AudioSeconds, double AccuracyScore, double FluencyScore, double? ProsodyScore,
    double PronunciationScore, IReadOnlyList<SpeakingWordIssue> WordIssues);

/// <summary>Speech to text with the pronunciation-assessment extension (Azure Speech).</summary>
public interface ISpeakingSpeechAssessor
{
    Task<SpeakingSpeechResult> AssessAsync(byte[] audio, string contentType, string locale,
        CancellationToken cancellationToken);
}

public sealed record SpeakingLanguageItem(int Position, int Part, int QuestionIndex, string ContentJson,
    string ReviewJson, string Transcript, double PronunciationScore, double FluencyScore, double? ProsodyScore,
    double? CompletenessScore, IReadOnlyList<SpeakingWordIssue> WordIssues);

public sealed record SpeakingLanguageWork(Guid EvaluationId, string Model, string RubricVersion,
    IReadOnlyList<SpeakingLanguageItem> Items);

/// <summary>Language-model scores are 0-100. Grammar and vocabulary are ignored for Part 1 (read aloud).</summary>
public sealed record SpeakingLanguageItemResult(int Position, int GrammarScore, int VocabularyScore,
    int RelevanceScore, string Feedback, string SuggestedResponse);

public sealed record SpeakingLanguageResult(string Summary, IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements, IReadOnlyList<SpeakingLanguageItemResult> Items);

/// <summary>Grades grammar, vocabulary and task relevance of the transcripts (LLM).</summary>
public interface ISpeakingLanguageEvaluator
{
    Task<SpeakingLanguageResult> EvaluateAsync(SpeakingLanguageWork work, CancellationToken cancellationToken);
}

/// <summary>A provider failure; <see cref="SafeMessage"/> is shown to learners, so it never carries details.</summary>
public sealed class SpeakingEvaluatorException(string failureCode, string safeMessage, bool isTransient,
    TimeSpan? retryAfter = null, Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string FailureCode { get; } = failureCode;
    public string SafeMessage { get; } = safeMessage;
    public bool IsTransient { get; } = isTransient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public interface ISpeakingEvaluationRepository
{
    Task<SpeakingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken);
    /// <summary>Evaluations without items, for attempt lists.</summary>
    Task<IReadOnlyDictionary<Guid, SpeakingEvaluation>> GetByAttemptIdsAsync(IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken);
    Task<SpeakingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<SpeakingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken);
    Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken);
    void Add(SpeakingEvaluation evaluation);
    /// <summary>Claims the next due job; returns null when nothing is due or another worker claimed it first.</summary>
    Task<SpeakingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken);
    /// <summary>Saves changes; a stale version raises <see cref="SpeakingConcurrencyException"/>.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record SpeakingWordIssueDto(string Word, double AccuracyScore);

public sealed record SpeakingEvaluationItemDto(int Position, int Part, string RecognitionStatus, string Transcript,
    double AccuracyScore, double FluencyScore, double? CompletenessScore, double? ProsodyScore,
    double PronunciationScore, int? GrammarScore, int? VocabularyScore, int? RelevanceScore, int RawScore,
    int MaxRawScore, string Feedback, string? SuggestedResponse, IReadOnlyList<SpeakingWordIssueDto> WordIssues);

/// <summary>Learner-facing result; per-question details are only returned once the job completed.</summary>
public sealed record SpeakingEvaluationDto(string Status, int? EstimatedScore, string? Summary,
    IReadOnlyList<string> Strengths, IReadOnlyList<string> Improvements, IReadOnlyList<SpeakingEvaluationItemDto> Items,
    string SpeechProvider, string LanguageProvider, string LanguageModel, string RubricVersion,
    DateTimeOffset ConsentAcceptedAtUtc, DateTimeOffset? QueuedAtUtc, DateTimeOffset? CompletedAtUtc, int RetryCount,
    string? FailureMessage);

public static class SpeakingEvaluationMapping
{
    public static SpeakingEvaluationDto ToDto(this SpeakingEvaluation evaluation)
    {
        var completed = evaluation.Status == SpeakingEvaluationStatus.Completed;
        return new SpeakingEvaluationDto(evaluation.Status.ToCode(), evaluation.EstimatedScore, evaluation.Summary,
            Strings(evaluation.StrengthsJson), Strings(evaluation.ImprovementsJson),
            completed ? evaluation.Items.Select(ToDto).ToArray() : [], evaluation.SpeechProvider,
            evaluation.LanguageProvider, evaluation.LanguageModel, evaluation.RubricVersion,
            evaluation.ConsentAcceptedAtUtc, evaluation.QueuedAtUtc, evaluation.CompletedAtUtc, evaluation.RetryCount,
            evaluation.FailureMessage);
    }

    public static IReadOnlyList<SpeakingWordIssue> WordIssues(this SpeakingEvaluationItem item) =>
        JsonSerializer.Deserialize<SpeakingWordIssue[]>(item.WordIssuesJson, SpeakingJson.Options) ?? [];

    private static SpeakingEvaluationItemDto ToDto(SpeakingEvaluationItem item) => new(item.Position, item.Part,
        item.RecognitionStatus.ToCode(), item.Transcript, item.AccuracyScore, item.FluencyScore,
        item.CompletenessScore, item.ProsodyScore, item.PronunciationScore, item.GrammarScore, item.VocabularyScore,
        item.RelevanceScore, item.RawScore ?? 0, item.MaxRawScore, item.Feedback ?? string.Empty,
        item.SuggestedResponse,
        item.WordIssues().Select(issue => new SpeakingWordIssueDto(issue.Word, issue.AccuracyScore)).ToArray());

    private static IReadOnlyList<string> Strings(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];
}
