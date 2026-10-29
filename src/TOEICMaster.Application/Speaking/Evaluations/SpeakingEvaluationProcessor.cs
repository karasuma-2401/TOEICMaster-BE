using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Evaluations;

public interface ISpeakingEvaluationProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Runs one job: Azure Speech transcribes and scores the pronunciation of every recording (saved per question),
/// then one language-model call grades grammar, vocabulary and relevance of all transcripts, then the server
/// combines everything into task scores and a 0-200 estimate.
/// </summary>
public sealed class SpeakingEvaluationProcessor(ISpeakingEvaluationRepository repository,
    ISpeakingMediaStore mediaStore, ISpeakingSpeechAssessor speechAssessor,
    ISpeakingLanguageEvaluator languageEvaluator, TimeProvider timeProvider) : ISpeakingEvaluationProcessor
{
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private const int MaximumRetries = 3;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var work = await repository.ClaimNextAsync(now, now.Add(Lease), cancellationToken);
        if (work is null)
            return false;

        try
        {
            var evaluation = await repository.FindForUpdateAsync(work.EvaluationId, cancellationToken);
            if (evaluation is null || evaluation.Status != SpeakingEvaluationStatus.Processing)
                return true;
            await AssessSpeechAsync(evaluation, work, cancellationToken);
            await GradeLanguageAsync(evaluation, work, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (SpeakingConcurrencyException)
        {
            // Another worker took the job over after the lease expired; it owns the result now.
        }
        catch (SpeakingEvaluatorException exception)
        {
            await RecordFailureAsync(work.EvaluationId, exception, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await RecordFailureAsync(work.EvaluationId, new SpeakingEvaluatorException("UNEXPECTED_ERROR",
                "Dịch vụ chấm AI đang gặp sự cố. Bài làm của bạn vẫn được lưu.", true), cancellationToken);
        }
        return true;
    }

    private async Task AssessSpeechAsync(SpeakingEvaluation evaluation, SpeakingEvaluationWork work,
        CancellationToken cancellationToken)
    {
        foreach (var question in work.Questions)
        {
            if (evaluation.FindItem(question.Position) is not null)
                continue;
            var maximum = SpeakingEvaluationScoring.MaxRawScore(question.Part);
            SpeakingEvaluationItem item;
            if (question.Recording is not { } recording)
            {
                item = SpeakingEvaluationItem.Unscorable(evaluation.Id, question.Position, question.Part,
                    SpeakingRecognitionStatus.NoRecording, maximum);
            }
            else
            {
                var audio = await mediaStore.DownloadAsync(SpeakingMediaKind.Recording, recording.ObjectKey,
                    cancellationToken);
                var speech = await speechAssessor.AssessAsync(audio, recording.ContentType, work.SpeechLocale,
                    cancellationToken);
                item = speech.Status == SpeakingRecognitionStatus.Recognized &&
                       !string.IsNullOrWhiteSpace(speech.Transcript)
                    ? SpeakingEvaluationItem.Recognized(evaluation.Id, question.Position, question.Part,
                        Truncate(speech.Transcript.Trim(), 5_000), speech.AudioSeconds, speech.AccuracyScore, speech.FluencyScore,
                        question.Part == 1 ? ReadingCompleteness(question, speech) : null, speech.ProsodyScore,
                        speech.PronunciationScore, SpeakingJson.Serialize(speech.WordIssues.Take(20).ToArray()),
                        maximum)
                    : SpeakingEvaluationItem.Unscorable(evaluation.Id, question.Position, question.Part,
                        SpeakingRecognitionStatus.NoSpeech, maximum);
            }
            var now = timeProvider.GetUtcNow();
            evaluation.RecordSpeech(item, now, now.Add(Lease));
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task GradeLanguageAsync(SpeakingEvaluation evaluation, SpeakingEvaluationWork work,
        CancellationToken cancellationToken)
    {
        var questions = work.Questions.ToDictionary(question => question.Position);
        var recognized = evaluation.Items.Where(item => item.RecognitionStatus == SpeakingRecognitionStatus.Recognized)
            .ToArray();
        SpeakingLanguageResult? result = null;
        if (recognized.Length > 0)
        {
            var languageWork = new SpeakingLanguageWork(evaluation.Id, work.LanguageModel, work.RubricVersion,
                recognized.Select(item =>
                {
                    var question = questions[item.Position];
                    return new SpeakingLanguageItem(item.Position, item.Part, question.QuestionIndex,
                        question.ContentJson, question.ReviewJson, item.Transcript, item.PronunciationScore,
                        item.FluencyScore, item.ProsodyScore, item.CompletenessScore, item.WordIssues());
                }).ToArray());
            result = await languageEvaluator.EvaluateAsync(languageWork, cancellationToken);
            Validate(result, recognized);
        }

        var byPosition = result?.Items.ToDictionary(item => item.Position) ?? [];
        foreach (var item in evaluation.Items)
        {
            if (!byPosition.TryGetValue(item.Position, out var language))
            {
                item.ApplyLanguageScores(null, null, null, 0, item.RecognitionStatus switch
                {
                    SpeakingRecognitionStatus.NoRecording => "Câu này không có bản ghi nên được 0 điểm.",
                    _ => "Không nhận diện được giọng nói trong bản ghi nên câu này được 0 điểm."
                }, null);
                continue;
            }
            var readAloud = item.Part == 1;
            int? grammar = readAloud ? null : language.GrammarScore;
            int? vocabulary = readAloud ? null : language.VocabularyScore;
            int? relevance = readAloud ? null : language.RelevanceScore;
            var quality = SpeakingEvaluationScoring.QualityScore(item.Part, item.PronunciationScore,
                item.CompletenessScore, language.GrammarScore, language.VocabularyScore, language.RelevanceScore);
            item.ApplyLanguageScores(grammar, vocabulary, relevance,
                SpeakingEvaluationScoring.RawScore(item.Part, quality, relevance), language.Feedback.Trim(),
                string.IsNullOrWhiteSpace(language.SuggestedResponse) ? null : language.SuggestedResponse.Trim());
        }

        evaluation.Complete(SpeakingEvaluationScoring.EstimatedScore(evaluation.Items),
            result?.Summary.Trim() ??
            "Hệ thống không nhận diện được câu trả lời nào trong các bản ghi, nên chưa thể đánh giá bài nói.",
            result?.Strengths.Select(text => text.Trim()).ToArray() ?? [],
            result?.Improvements.Select(text => text.Trim()).ToArray() ??
            ["Kiểm tra micro và nói rõ, đủ lớn trong thời gian trả lời."], timeProvider.GetUtcNow());
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    private static double? ReadingCompleteness(SpeakingEvaluationQuestion question, SpeakingSpeechResult speech)
    {
        var passage = SpeakingJson.Deserialize<SpeakingContentDto>(question.ContentJson).Passage;
        return string.IsNullOrWhiteSpace(passage)
            ? null
            : SpeakingEvaluationScoring.ReadingCompleteness(passage,
                string.IsNullOrWhiteSpace(speech.LexicalTranscript) ? speech.Transcript : speech.LexicalTranscript);
    }

    private static void Validate(SpeakingLanguageResult result, IReadOnlyCollection<SpeakingEvaluationItem> recognized)
    {
        var expected = recognized.Select(item => item.Position).ToHashSet();
        if (string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 2_000 ||
            result.Strengths.Count > 5 || result.Improvements.Count > 5 ||
            result.Strengths.Concat(result.Improvements).Any(text => string.IsNullOrWhiteSpace(text) || text.Length > 400) ||
            result.Items.Count != expected.Count || !result.Items.Select(item => item.Position).ToHashSet().SetEquals(expected) ||
            result.Items.Any(item => item.GrammarScore is < 0 or > 100 || item.VocabularyScore is < 0 or > 100 ||
                                     item.RelevanceScore is < 0 or > 100 || string.IsNullOrWhiteSpace(item.Feedback) ||
                                     item.Feedback.Length > 1_500 || item.SuggestedResponse.Length > 1_500))
            throw new SpeakingEvaluatorException("INVALID_RESPONSE",
                "Dịch vụ chấm AI trả về kết quả không hợp lệ. Hệ thống sẽ thử lại.", true);
    }

    private async Task RecordFailureAsync(Guid evaluationId, SpeakingEvaluatorException exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var evaluation = await repository.FindForUpdateAsync(evaluationId, cancellationToken);
            if (evaluation is null || evaluation.Status != SpeakingEvaluationStatus.Processing)
                return;
            var now = timeProvider.GetUtcNow();
            if (exception.IsTransient && evaluation.RetryCount < MaximumRetries)
            {
                var defaultDelay = TimeSpan.FromMinutes(Math.Pow(3, evaluation.RetryCount));
                var delay = exception.RetryAfter is { } retryAfter && retryAfter > defaultDelay ? retryAfter : defaultDelay;
                evaluation.ScheduleRetry(exception.FailureCode, exception.SafeMessage, now.Add(delay), now);
            }
            else
            {
                evaluation.Fail(exception.FailureCode, exception.SafeMessage, now);
            }
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (SpeakingConcurrencyException)
        {
            // The lease moved to another worker, which records its own outcome.
        }
    }
}
