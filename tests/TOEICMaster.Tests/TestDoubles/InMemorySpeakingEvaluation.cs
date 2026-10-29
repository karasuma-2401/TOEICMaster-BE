using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

/// <summary>In-memory Speaking evaluation jobs reading attempts and recordings from an <see cref="InMemorySpeaking"/>.</summary>
public sealed class InMemorySpeakingEvaluations(InMemorySpeaking speaking) : ISpeakingEvaluationRepository
{
    private readonly Lock _gate = new();
    public List<SpeakingEvaluation> Evaluations { get; } = [];

    public Task<SpeakingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(evaluation => evaluation.AttemptId == attemptId));

    public Task<IReadOnlyDictionary<Guid, SpeakingEvaluation>> GetByAttemptIdsAsync(
        IReadOnlyCollection<Guid> attemptIds, CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyDictionary<Guid, SpeakingEvaluation>>(Evaluations
                .Where(evaluation => attemptIds.Contains(evaluation.AttemptId))
                .ToDictionary(evaluation => evaluation.AttemptId));
    }

    public Task<SpeakingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId,
        CancellationToken cancellationToken) => GetByAttemptIdAsync(attemptId, cancellationToken);

    public Task<SpeakingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(evaluation => evaluation.Id == evaluationId));

    public Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(Evaluations.Any(evaluation => evaluation.AttemptId != excludingAttemptId &&
                evaluation.ConsentAcceptedAtUtc >= afterUtc && speaking.Attempts.Any(attempt =>
                    attempt.Id == evaluation.AttemptId && attempt.AnonymousSessionHash == anonymousSessionHash)));
    }

    public void Add(SpeakingEvaluation evaluation)
    {
        lock (_gate)
        {
            if (Evaluations.Any(existing => existing.AttemptId == evaluation.AttemptId))
                throw new SpeakingConflictException("Yêu cầu chấm AI cho lượt làm này đã được gửi.");
            Evaluations.Add(evaluation);
        }
    }

    public Task<SpeakingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var candidate = Evaluations.Where(evaluation => evaluation.CanBeClaimed(now))
                .OrderBy(evaluation => evaluation.CreatedAtUtc).FirstOrDefault();
            if (candidate is null)
                return Task.FromResult<SpeakingEvaluationWork?>(null);
            candidate.BeginProcessing(now, leaseExpiresAtUtc);
            var attempt = speaking.Attempts.Single(item => item.Id == candidate.AttemptId);
            var questions = attempt.Questions.Select(question =>
            {
                var asset = speaking.Assets.SingleOrDefault(item => item.Id == question.RecordingAssetId);
                return new SpeakingEvaluationQuestion(question.Position, question.Part, question.QuestionIndex,
                    question.ContentJson, question.ReviewJson,
                    asset is null ? null : new SpeakingRecordingReference(asset.ObjectKey, asset.ContentType));
            }).ToArray();
            return Task.FromResult<SpeakingEvaluationWork?>(new SpeakingEvaluationWork(candidate.Id,
                candidate.SpeechLocale, candidate.LanguageModel, candidate.RubricVersion, questions));
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private SpeakingEvaluation? Find(Func<SpeakingEvaluation, bool> predicate)
    {
        lock (_gate)
            return Evaluations.SingleOrDefault(predicate);
    }
}

public sealed class StaticSpeakingEvaluationSettings : ISpeakingEvaluationSettings
{
    public bool IsConfigured { get; set; } = true;
    public string SpeechProvider => "azure-speech";
    public string SpeechLocale => "en-US";
    public string LanguageProvider => "gemini";
    public string LanguageModel => "gemini-test";
    public string RubricVersion => "toeic-speaking-v1";
}

/// <summary>Returns <see cref="Result"/> for every recording, or throws <see cref="Failure"/> when set.</summary>
public sealed class FakeSpeechAssessor : ISpeakingSpeechAssessor
{
    public Func<byte[], SpeakingSpeechResult> Result { get; set; } = _ => Recognized("I would like to book a table.");
    public SpeakingEvaluatorException? Failure { get; set; }
    public int Calls { get; private set; }

    public static SpeakingSpeechResult Recognized(string transcript, double pronunciation = 80,
        string? lexical = null) => new(SpeakingRecognitionStatus.Recognized, transcript, lexical ?? transcript, 12,
        pronunciation, pronunciation, pronunciation, pronunciation, [new SpeakingWordIssue("table", 41)]);

    public Task<SpeakingSpeechResult> AssessAsync(byte[] audio, string contentType, string locale,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is not null ? Task.FromException<SpeakingSpeechResult>(Failure) : Task.FromResult(Result(audio));
    }
}

/// <summary>Scores every item with the same grammar, vocabulary and relevance unless <see cref="Failure"/> is set.</summary>
public sealed class FakeLanguageEvaluator : ISpeakingLanguageEvaluator
{
    public int Score { get; set; } = 80;
    public int Relevance { get; set; } = 80;
    public SpeakingEvaluatorException? Failure { get; set; }
    public Func<SpeakingLanguageWork, SpeakingLanguageResult>? Override { get; set; }
    public List<SpeakingLanguageWork> Calls { get; } = [];

    public Task<SpeakingLanguageResult> EvaluateAsync(SpeakingLanguageWork work, CancellationToken cancellationToken)
    {
        Calls.Add(work);
        if (Failure is not null)
            return Task.FromException<SpeakingLanguageResult>(Failure);
        return Task.FromResult(Override?.Invoke(work) ?? new SpeakingLanguageResult("Bài nói khá tốt.",
            ["Phát âm rõ."], ["Mở rộng ý."], work.Items.Select(item => new SpeakingLanguageItemResult(item.Position,
                Score, Score, Relevance, $"Nhận xét câu {item.Position}.", item.Part == 1 ? "" : "Better answer."))
                .ToArray()));
    }
}
