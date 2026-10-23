using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Evaluations;

public interface IWritingEvaluationProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

public sealed class WritingEvaluationProcessor(IWritingEvaluationRepository repository, IWritingEvaluator evaluator,
    TimeProvider timeProvider) : IWritingEvaluationProcessor
{
    private const int MaximumRetries = 3;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var work = await repository.ClaimNextAsync(now, now.AddMinutes(2), cancellationToken);
        if (work is null)
            return false;

        try
        {
            var result = await evaluator.EvaluateAsync(work, cancellationToken);
            ValidateResult(result, work);
            var evaluation = await repository.FindForUpdateAsync(work.EvaluationId, cancellationToken);
            if (evaluation is null || evaluation.Status != WritingEvaluationStatus.Processing)
                return true;
            var feedback = result.Feedback.Select(item => WritingEvaluationFeedback.Create(evaluation.Id,
                item.QuestionPosition, item.CriterionScore, item.Feedback.Trim(), item.SuggestedRevision.Trim())).ToArray();
            evaluation.Complete(ToEstimatedToeicScore(work, result), result.Summary.Trim(), result.Strengths.Select(item => item.Trim()).ToArray(),
                result.Improvements.Select(item => item.Trim()).ToArray(), feedback, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (WritingEvaluatorException exception)
        {
            await RecordFailureAsync(work.EvaluationId, exception, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await RecordFailureAsync(work.EvaluationId, new WritingEvaluatorException("UNEXPECTED_ERROR",
                "Dịch vụ chấm AI đang gặp sự cố. Bài làm của bạn vẫn được lưu.", true), cancellationToken);
        }
        return true;
    }

    private async Task RecordFailureAsync(Guid evaluationId, WritingEvaluatorException exception,
        CancellationToken cancellationToken)
    {
        var evaluation = await repository.FindForUpdateAsync(evaluationId, cancellationToken);
        if (evaluation is null || evaluation.Status != WritingEvaluationStatus.Processing)
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

    private static void ValidateResult(WritingEvaluatorResult result, WritingEvaluationWork work)
    {
        if (string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 2_000 || result.Strengths.Count > 5 ||
            result.Improvements.Count > 5 || result.Feedback.Count != work.Answers.Count ||
            result.Strengths.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 400) ||
            result.Improvements.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 400) ||
            result.Feedback.Select(item => item.QuestionPosition).Distinct().Count() != result.Feedback.Count ||
            result.Feedback.Any(item => !work.Answers.Any(answer => answer.Position == item.QuestionPosition) ||
                item.CriterionScore is < 0 or > 5 || item.CriterionScore > MaxRawScore(work.Answers.Single(answer =>
                    answer.Position == item.QuestionPosition).Part) ||
                string.IsNullOrWhiteSpace(item.Feedback) || item.Feedback.Length > 1_500 ||
                string.IsNullOrWhiteSpace(item.SuggestedRevision) || item.SuggestedRevision.Length > 1_500))
            throw new WritingEvaluatorException("INVALID_RESPONSE", "Dịch vụ chấm AI trả về kết quả không hợp lệ.", true);
    }

    private static int ToEstimatedToeicScore(WritingEvaluationWork work, WritingEvaluatorResult result)
    {
        var rawScore = result.Feedback.Sum(item => item.CriterionScore);
        var maximumScore = work.Answers.Sum(answer => MaxRawScore(answer.Part));
        return (int)Math.Round(rawScore * 20d / maximumScore, MidpointRounding.AwayFromZero) * 10;
    }

    private static int MaxRawScore(int part) => part switch
    {
        1 => 3,
        2 => 4,
        3 => 5,
        _ => throw new WritingEvaluatorException("INVALID_RESPONSE", "Dữ liệu câu hỏi Writing không hợp lệ.", false)
    };
}
