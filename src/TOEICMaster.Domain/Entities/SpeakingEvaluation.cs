using System.Text.Json;

namespace TOEICMaster.Domain.Entities;

public enum SpeakingEvaluationStatus
{
    Queued = 1,
    Processing = 2,
    Retrying = 3,
    Completed = 4,
    Failed = 5
}

public enum SpeakingRecognitionStatus
{
    Recognized = 1,
    NoSpeech = 2,
    NoRecording = 3
}

/// <summary>
/// Durable AI-evaluation job for a submitted Speaking attempt. Azure Speech results are stored per question as soon
/// as they are known, so a retry after a language-model failure does not transcribe the recordings again.
/// </summary>
public sealed class SpeakingEvaluation
{
    private readonly List<SpeakingEvaluationItem> _items = [];

    private SpeakingEvaluation()
    {
    }

    private SpeakingEvaluation(Guid id, Guid attemptId, string speechProvider, string speechLocale,
        string languageProvider, string languageModel, string rubricVersion, DateTimeOffset now)
    {
        Id = id;
        AttemptId = attemptId;
        SpeechProvider = speechProvider;
        SpeechLocale = speechLocale;
        LanguageProvider = languageProvider;
        LanguageModel = languageModel;
        RubricVersion = rubricVersion;
        Status = SpeakingEvaluationStatus.Queued;
        ConsentAcceptedAtUtc = now;
        QueuedAtUtc = now;
        NextAttemptAtUtc = now;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public SpeakingEvaluationStatus Status { get; private set; }
    public string SpeechProvider { get; private set; } = string.Empty;
    public string SpeechLocale { get; private set; } = string.Empty;
    public string LanguageProvider { get; private set; } = string.Empty;
    public string LanguageModel { get; private set; } = string.Empty;
    public string RubricVersion { get; private set; } = string.Empty;
    public DateTimeOffset ConsentAcceptedAtUtc { get; private set; }
    public DateTimeOffset? QueuedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public int? EstimatedScore { get; private set; }
    public string? Summary { get; private set; }
    public string? StrengthsJson { get; private set; }
    public string? ImprovementsJson { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyList<SpeakingEvaluationItem> Items => _items.OrderBy(item => item.Position).ToArray();

    /// <summary>Creating the job records the learner's explicit consent and queues it.</summary>
    public static SpeakingEvaluation Create(Guid attemptId, string speechProvider, string speechLocale,
        string languageProvider, string languageModel, string rubricVersion, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speechProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(speechLocale);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubricVersion);
        return new SpeakingEvaluation(Guid.NewGuid(), attemptId, speechProvider, speechLocale, languageProvider,
            languageModel, rubricVersion, now);
    }

    /// <summary>Queues a failed job again; speech results already stored are reused.</summary>
    public bool Requeue(DateTimeOffset now)
    {
        if (Status != SpeakingEvaluationStatus.Failed)
            return false;
        Status = SpeakingEvaluationStatus.Queued;
        QueuedAtUtc = now;
        NextAttemptAtUtc = now;
        RetryCount = 0;
        FailureCode = null;
        FailureMessage = null;
        Touch(now);
        return true;
    }

    public bool CanBeClaimed(DateTimeOffset now) =>
        Status is SpeakingEvaluationStatus.Queued or SpeakingEvaluationStatus.Retrying &&
        NextAttemptAtUtc is not null && NextAttemptAtUtc <= now ||
        Status == SpeakingEvaluationStatus.Processing && LeaseExpiresAtUtc is not null && LeaseExpiresAtUtc <= now;

    public void BeginProcessing(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc)
    {
        if (!CanBeClaimed(now))
            throw new InvalidOperationException("This Speaking evaluation is not ready to be processed.");
        Status = SpeakingEvaluationStatus.Processing;
        StartedAtUtc ??= now;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        NextAttemptAtUtc = null;
        Touch(now);
    }

    public SpeakingEvaluationItem? FindItem(int position) => _items.SingleOrDefault(item => item.Position == position);

    /// <summary>Stores the speech result of one question and extends the lease while the job keeps progressing.</summary>
    public void RecordSpeech(SpeakingEvaluationItem item, DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc)
    {
        EnsureProcessing();
        if (item.EvaluationId != Id)
            throw new InvalidOperationException("The Speaking evaluation item belongs to another evaluation.");
        if (FindItem(item.Position) is { } existing)
            _items.Remove(existing);
        _items.Add(item);
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        Touch(now);
    }

    public void Complete(int estimatedScore, string summary, IReadOnlyList<string> strengths,
        IReadOnlyList<string> improvements, DateTimeOffset now)
    {
        EnsureProcessing();
        if (estimatedScore is < 0 or > 200 || estimatedScore % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedScore),
                "Speaking scores must be between 0 and 200 in steps of 10.");
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (_items.Count == 0 || _items.Any(item => item.RawScore is null))
            throw new InvalidOperationException("Every Speaking question must be scored before completing.");

        EstimatedScore = estimatedScore;
        Summary = summary;
        StrengthsJson = JsonSerializer.Serialize(strengths);
        ImprovementsJson = JsonSerializer.Serialize(improvements);
        Status = SpeakingEvaluationStatus.Completed;
        CompletedAtUtc = now;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FailureCode = null;
        FailureMessage = null;
        Touch(now);
    }

    public void ScheduleRetry(string failureCode, string failureMessage, DateTimeOffset nextAttemptAtUtc,
        DateTimeOffset now)
    {
        EnsureProcessing();
        RetryCount++;
        Status = SpeakingEvaluationStatus.Retrying;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        Touch(now);
    }

    public void Fail(string failureCode, string failureMessage, DateTimeOffset now)
    {
        EnsureProcessing();
        RetryCount++;
        Status = SpeakingEvaluationStatus.Failed;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        Touch(now);
    }

    private void EnsureProcessing()
    {
        if (Status != SpeakingEvaluationStatus.Processing)
            throw new InvalidOperationException("Only an active Speaking evaluation can be updated.");
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        Version++;
    }
}

/// <summary>
/// One question of an evaluation: Azure Speech transcript and pronunciation scores (0-100), then the language
/// model's grammar, vocabulary and relevance scores (0-100) and the TOEIC raw score derived from all of them.
/// </summary>
public sealed class SpeakingEvaluationItem
{
    private SpeakingEvaluationItem()
    {
    }

    public Guid EvaluationId { get; private set; }
    public int Position { get; private set; }
    public int Part { get; private set; }
    public SpeakingRecognitionStatus RecognitionStatus { get; private set; }
    public string Transcript { get; private set; } = string.Empty;
    public double AudioSeconds { get; private set; }
    public double AccuracyScore { get; private set; }
    public double FluencyScore { get; private set; }
    /// <summary>Share of the Part 1 passage that was read; other parts have no script.</summary>
    public double? CompletenessScore { get; private set; }
    /// <summary>Stress, intonation and rhythm; only returned for en-US.</summary>
    public double? ProsodyScore { get; private set; }
    public double PronunciationScore { get; private set; }
    /// <summary>Words Azure marked as mispronounced, as a JSON array of { word, accuracyScore }.</summary>
    public string WordIssuesJson { get; private set; } = "[]";
    public int? GrammarScore { get; private set; }
    public int? VocabularyScore { get; private set; }
    public int? RelevanceScore { get; private set; }
    public int? RawScore { get; private set; }
    public int MaxRawScore { get; private set; }
    public string? Feedback { get; private set; }
    public string? SuggestedResponse { get; private set; }

    public static SpeakingEvaluationItem Unscorable(Guid evaluationId, int position, int part,
        SpeakingRecognitionStatus status, int maxRawScore)
    {
        if (status == SpeakingRecognitionStatus.Recognized)
            throw new ArgumentOutOfRangeException(nameof(status), "A recognized answer needs speech scores.");
        return new SpeakingEvaluationItem
        {
            EvaluationId = evaluationId,
            Position = position,
            Part = part,
            RecognitionStatus = status,
            MaxRawScore = maxRawScore
        };
    }

    public static SpeakingEvaluationItem Recognized(Guid evaluationId, int position, int part, string transcript,
        double audioSeconds, double accuracyScore, double fluencyScore, double? completenessScore,
        double? prosodyScore, double pronunciationScore, string wordIssuesJson, int maxRawScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        return new SpeakingEvaluationItem
        {
            EvaluationId = evaluationId,
            Position = position,
            Part = part,
            RecognitionStatus = SpeakingRecognitionStatus.Recognized,
            Transcript = transcript,
            AudioSeconds = audioSeconds,
            AccuracyScore = Score(accuracyScore),
            FluencyScore = Score(fluencyScore),
            CompletenessScore = completenessScore is { } completeness ? Score(completeness) : null,
            ProsodyScore = prosodyScore is { } prosody ? Score(prosody) : null,
            PronunciationScore = Score(pronunciationScore),
            WordIssuesJson = wordIssuesJson,
            MaxRawScore = maxRawScore
        };
    }

    public void ApplyLanguageScores(int? grammarScore, int? vocabularyScore, int? relevanceScore, int rawScore,
        string feedback, string? suggestedResponse)
    {
        if (rawScore < 0 || rawScore > MaxRawScore)
            throw new ArgumentOutOfRangeException(nameof(rawScore));
        ArgumentException.ThrowIfNullOrWhiteSpace(feedback);
        GrammarScore = grammarScore;
        VocabularyScore = vocabularyScore;
        RelevanceScore = relevanceScore;
        RawScore = rawScore;
        Feedback = feedback;
        SuggestedResponse = suggestedResponse;
    }

    private static double Score(double value) => Math.Round(Math.Clamp(value, 0, 100), 1);
}
