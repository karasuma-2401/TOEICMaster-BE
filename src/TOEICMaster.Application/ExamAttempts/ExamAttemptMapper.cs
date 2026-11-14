using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

/// <summary>Builds attempt DTOs from domain objects. Never exposes keys to the learner before submission.</summary>
public static class ExamAttemptMapper
{
    public const int MinScaledScore = 10;
    public const int MaxSkillScaledScore = 495;
    public const int MaxScaledScore = MaxSkillScaledScore * 2;

    public static string PhaseOf(ExamAttempt attempt) =>
        attempt.IsListeningLocked ? ExamAttemptPhases.Reading : ExamAttemptPhases.Listening;

    private static IEnumerable<ExamBlock> Ordered(ExamRevision revision) =>
        revision.Blocks.OrderBy(block => block.Part).ThenBy(block => block.FirstQuestionNumber);

    private static List<ExamContextBlockDto> Contexts(ExamBlock block) =>
        block.ContextBlocks
            .OrderBy(context => context.Ordinal)
            .Select(context => new ExamContextBlockDto(context.Id, context.Ordinal, context.Text, context.ImageAssetId))
            .ToList();

    public static int CountQuestions(ExamRevision revision) => revision.Blocks.Sum(block => block.Questions.Count);

    public static int CountListeningQuestions(ExamRevision revision) =>
        revision.Blocks.Where(block => ExamAttempt.IsListeningPart(block.Part)).Sum(block => block.Questions.Count);

    public static ExamAttemptSheetDto ToSheet(
        ExamAttempt attempt,
        ExamRevision revision,
        ExamHeader header,
        DateTimeOffset now)
    {
        var saved = attempt.Answers.ToDictionary(answer => answer.QuestionId);

        var blocks = Ordered(revision).Select(block => new ExamBlockSheetDto(
            block.Id,
            block.Part,
            block.FirstQuestionNumber,
            block.AudioAssetId,
            Contexts(block),
            block.Questions.OrderBy(question => question.Number).Select(question =>
            {
                saved.TryGetValue(question.Id, out var answer);
                return new ExamQuestionSheetDto(
                    question.Id,
                    question.Number,
                    question.Stem,
                    question.OptionA,
                    question.OptionB,
                    question.OptionC,
                    question.OptionD,
                    question.ImageAssetId,
                    answer?.SelectedKey,
                    answer?.IsFlagged ?? false);
            }).ToList())).ToList();

        return new ExamAttemptSheetDto(
            attempt.Id,
            header.Id,
            header.Code,
            header.Title,
            revision.VersionNumber,
            revision.MasterAudioObjectKey,
            PhaseOf(attempt),
            attempt.StartedAtUtc,
            attempt.ReadingStartedAtUtc,
            attempt.TimeLimitSeconds,
            attempt.GetRemainingSeconds(now),
            CountQuestions(revision),
            blocks);
    }

    private static bool IsSkipped(ExamAttemptAnswer? answer) => string.IsNullOrWhiteSpace(answer?.SelectedKey);

    public static string StatusOf(ExamAttemptAnswer? answer) =>
        IsSkipped(answer)
            ? ExamReviewStatuses.Skipped
            : answer!.IsCorrect == true
                ? ExamReviewStatuses.Correct
                : ExamReviewStatuses.Wrong;

    public static ExamAttemptResultDto ToResult(ExamAttempt attempt, ExamHeader header)
    {
        var parts = attempt.Answers
            .GroupBy(answer => answer.Part)
            .OrderBy(group => group.Key)
            .Select(group => new PartScoreSummaryDto(
                group.Key,
                group.Count(),
                group.Count(answer => StatusOf(answer) == ExamReviewStatuses.Correct),
                group.Count(answer => StatusOf(answer) == ExamReviewStatuses.Wrong),
                group.Count(answer => StatusOf(answer) == ExamReviewStatuses.Skipped)))
            .ToList();

        var submittedAt = attempt.SubmittedAtUtc ?? attempt.StartedAtUtc;

        return new ExamAttemptResultDto(
            attempt.Id,
            header.Id,
            header.Code,
            header.Title,
            attempt.ListeningRawScore ?? 0,
            attempt.ReadingRawScore ?? 0,
            attempt.ListeningScaledScore ?? 0,
            attempt.ReadingScaledScore ?? 0,
            attempt.TotalScaledScore ?? 0,
            MaxSkillScaledScore,
            MaxSkillScaledScore,
            MaxScaledScore,
            parts.Sum(part => part.CorrectCount),
            parts.Sum(part => part.IncorrectCount),
            parts.Sum(part => part.SkippedCount),
            attempt.IsTimeExpired,
            attempt.StartedAtUtc,
            submittedAt,
            Math.Max(0, (int)(submittedAt - attempt.StartedAtUtc).TotalSeconds),
            parts);
    }

    public static ExamAttemptReviewDto ToReview(
        ExamAttempt attempt,
        ExamRevision revision,
        ExamHeader header,
        ExamReviewFilter filter)
    {
        var answers = attempt.Answers.ToDictionary(answer => answer.QuestionId);

        var counts = new ExamAttemptReviewCountsDto(
            answers.Values.Count(answer => StatusOf(answer) == ExamReviewStatuses.Correct),
            answers.Values.Count(answer => StatusOf(answer) == ExamReviewStatuses.Wrong),
            answers.Values.Count(answer => StatusOf(answer) == ExamReviewStatuses.Skipped),
            answers.Values.Count(answer => answer.IsFlagged));

        var blocks = new List<ExamAttemptReviewBlockDto>();
        foreach (var block in Ordered(revision))
        {
            var questions = new List<ExamAttemptReviewQuestionDto>();
            foreach (var question in block.Questions.OrderBy(candidate => candidate.Number))
            {
                answers.TryGetValue(question.Id, out var answer);
                var status = StatusOf(answer);
                var flagged = answer?.IsFlagged ?? false;

                var included = filter switch
                {
                    ExamReviewFilter.Incorrect => status != ExamReviewStatuses.Correct,
                    ExamReviewFilter.Flagged => flagged,
                    _ => true
                };
                if (!included) continue;

                questions.Add(new ExamAttemptReviewQuestionDto(
                    question.Id,
                    question.Number,
                    block.Part,
                    question.Stem,
                    question.OptionA,
                    question.OptionB,
                    question.OptionC,
                    question.OptionD,
                    answer?.SelectedKey,
                    question.CorrectKey,
                    status,
                    flagged,
                    question.Explanation,
                    question.Translation,
                    question.ImageAssetId));
            }

            if (questions.Count == 0) continue;
            blocks.Add(new ExamAttemptReviewBlockDto(block.Id, block.Part, block.FirstQuestionNumber, Contexts(block), questions));
        }

        return new ExamAttemptReviewDto(
            attempt.Id,
            header.Id,
            header.Code,
            header.Title,
            attempt.TotalScaledScore ?? 0,
            attempt.ListeningScaledScore ?? 0,
            attempt.ReadingScaledScore ?? 0,
            attempt.IsTimeExpired,
            counts,
            blocks);
    }
}
