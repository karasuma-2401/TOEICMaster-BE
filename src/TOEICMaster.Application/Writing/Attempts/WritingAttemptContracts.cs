using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Attempts;

public sealed record WritingAttemptOwner(Guid? UserId, string? AnonymousSessionHash)
{
    public static WritingAttemptOwner ForUser(Guid userId) => new(userId, null);

    public static WritingAttemptOwner ForAnonymousSession(string anonymousSessionId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(anonymousSessionId));
        return new(null, Convert.ToHexString(bytes));
    }
}

public sealed record StartWritingAttemptInput(Guid TestId, string Mode);

public sealed record SaveWritingAnswerInput(string Answer);

public sealed record WritingAttemptPartDto(int Part, int QuestionCount, int PreparationSeconds, int ResponseSeconds);

public sealed record WritingPublicTestSummaryDto(Guid Id, string Code, string Title, string Description,
    IReadOnlyList<WritingAttemptPartDto> Parts, DateTimeOffset PublishedAtUtc);

public sealed record WritingPublicTestPageDto(IReadOnlyList<WritingPublicTestSummaryDto> Items, int PageNumber,
    int PageSize, int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record WritingPublicTestDto(Guid Id, string Code, string Title, string Description,
    IReadOnlyList<WritingAttemptPartDto> Parts, DateTimeOffset PublishedAtUtc);

public sealed record WritingAttemptQuestionDto(int Position, int Part, int PreparationSeconds, int ResponseSeconds,
    string? ImageUrl, string? ImageAlt, string? KeywordOne, string? KeywordTwo, string? Sender, string? Recipient,
    string? Subject, string? EmailBody, string? Directions, string? Prompt, int? MinimumWordCount, string? Outline,
    string Answer, DateTimeOffset? LastSavedAtUtc, DateTimeOffset? LockedAtUtc);

public sealed record WritingAttemptPromptSnapshot(int Part, string? ImageAlt, Guid? ImageAssetId,
    string? KeywordOne, string? KeywordTwo, string? Sender, string? Recipient, string? Subject, string? EmailBody,
    string? Directions, string? Prompt, int? MinimumWordCount, string? Outline);

public sealed record WritingAttemptReviewSnapshot(string? ModelAnswer, string? RubricNotes);

public sealed record WritingAttemptReviewQuestionDto(int Position, string ModelAnswer);

public sealed record WritingAttemptDto(Guid Id, Guid TestId, string TestCode, string TestTitle, string Mode,
    string Status, int? CurrentQuestionPosition, string? CurrentPhase, DateTimeOffset? PhaseEndsAtUtc,
    DateTimeOffset ServerNowUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? SubmittedAtUtc,
    IReadOnlyList<WritingAttemptQuestionDto> Questions, WritingEvaluationDto? Evaluation,
    IReadOnlyList<WritingAttemptReviewQuestionDto> ReviewQuestions);

public sealed record WritingEvaluationFeedbackDto(int QuestionPosition, int CriterionScore, string Feedback,
    string SuggestedRevision);

public sealed record WritingEvaluationDto(string Status, int? EstimatedScore, string? Summary,
    IReadOnlyList<string> Strengths, IReadOnlyList<string> Improvements,
    IReadOnlyList<WritingEvaluationFeedbackDto> Feedback, string Provider, string Model, string RubricVersion,
    DateTimeOffset? ConsentAcceptedAtUtc, DateTimeOffset? QueuedAtUtc, DateTimeOffset? CompletedAtUtc,
    int RetryCount, string? FailureMessage);

public sealed record WritingAttemptSummaryDto(Guid Id, Guid TestId, string TestCode, string TestTitle, string Mode,
    string Status, int QuestionCount, int? CurrentQuestionPosition, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc, DateTimeOffset UpdatedAtUtc, string? EvaluationStatus, int? EstimatedScore);

public sealed record WritingAttemptPageDto(IReadOnlyList<WritingAttemptSummaryDto> Items);

public static class WritingAttemptMapping
{
    public static WritingAttemptMode ParseMode(string mode) => mode.Trim().ToUpperInvariant() switch
    {
        "FULL" => WritingAttemptMode.Full,
        "PART_1" => WritingAttemptMode.Part1,
        "PART_2" => WritingAttemptMode.Part2,
        "PART_3" => WritingAttemptMode.Part3,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static string ToApiValue(this WritingAttemptMode mode) => mode switch
    {
        WritingAttemptMode.Full => "FULL",
        WritingAttemptMode.Part1 => "PART_1",
        WritingAttemptMode.Part2 => "PART_2",
        WritingAttemptMode.Part3 => "PART_3",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static string ToApiValue(this WritingAttemptStatus status) => status switch
    {
        WritingAttemptStatus.InProgress => "IN_PROGRESS",
        WritingAttemptStatus.SubmittedAwaitingEvaluation => "SUBMITTED_AWAITING_EVALUATION",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToApiValue(this WritingAttemptPhase phase) => phase switch
    {
        WritingAttemptPhase.Preparing => "PREPARING",
        WritingAttemptPhase.Answering => "ANSWERING",
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    public static string ToApiValue(this WritingEvaluationStatus status) => status switch
    {
        WritingEvaluationStatus.ConsentRequired => "CONSENT_REQUIRED",
        WritingEvaluationStatus.Queued => "QUEUED",
        WritingEvaluationStatus.Processing => "PROCESSING",
        WritingEvaluationStatus.Retrying => "RETRYING",
        WritingEvaluationStatus.Completed => "COMPLETED",
        WritingEvaluationStatus.Failed => "FAILED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static WritingPart? PartForMode(this WritingAttemptMode mode) => mode switch
    {
        WritingAttemptMode.Full => null,
        WritingAttemptMode.Part1 => WritingPart.Part1,
        WritingAttemptMode.Part2 => WritingPart.Part2,
        WritingAttemptMode.Part3 => WritingPart.Part3,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static (int PreparationSeconds, int ResponseSeconds) TimingFor(this WritingPart part) => part switch
    {
        WritingPart.Part1 => (30, 60),
        WritingPart.Part2 => (60, 540),
        WritingPart.Part3 => (120, 1680),
        _ => throw new ArgumentOutOfRangeException(nameof(part))
    };

    public static WritingAttemptPartDto ToPartDto(this IGrouping<WritingPart, WritingTestQuestion> questions)
    {
        var timing = questions.Key.TimingFor();
        return new WritingAttemptPartDto((int)questions.Key, questions.Count(), timing.PreparationSeconds,
            timing.ResponseSeconds);
    }

    public static WritingAttemptQuestionDto ToDto(this WritingAttemptQuestion question)
    {
        var snapshot = JsonSerializer.Deserialize<WritingAttemptPromptSnapshot>(question.PromptSnapshotJson)
            ?? throw new InvalidOperationException("Invalid Writing attempt question snapshot.");
        return new WritingAttemptQuestionDto(question.Position, (int)question.Part, question.PreparationSeconds,
            question.ResponseSeconds, snapshot.ImageAssetId is null ? null : $"/api/v1/writing/media/{snapshot.ImageAssetId}",
            snapshot.ImageAlt, snapshot.KeywordOne, snapshot.KeywordTwo, snapshot.Sender, snapshot.Recipient,
            snapshot.Subject, snapshot.EmailBody, snapshot.Directions, snapshot.Prompt, snapshot.MinimumWordCount,
            snapshot.Outline, question.Answer, question.LastSavedAtUtc, question.LockedAtUtc);
    }

    public static string ToPromptSnapshotJson(string frozenTestQuestionSnapshot)
    {
        var snapshot = JsonSerializer.Deserialize<WritingExerciseSnapshot>(frozenTestQuestionSnapshot)
            ?? throw new InvalidOperationException("Invalid Writing test question snapshot.");
        return JsonSerializer.Serialize(new WritingAttemptPromptSnapshot(snapshot.Part, snapshot.ImageAlt,
            snapshot.ImageAssetId, snapshot.KeywordOne, snapshot.KeywordTwo, snapshot.Sender, snapshot.Recipient,
            snapshot.Subject, snapshot.EmailBody, snapshot.Directions, snapshot.Prompt, snapshot.MinimumWordCount,
            snapshot.Outline));
    }

    public static string ToReviewSnapshotJson(string frozenTestQuestionSnapshot)
    {
        var snapshot = JsonSerializer.Deserialize<WritingExerciseSnapshot>(frozenTestQuestionSnapshot)
            ?? throw new InvalidOperationException("Invalid Writing test question snapshot.");
        return JsonSerializer.Serialize(new WritingAttemptReviewSnapshot(snapshot.ModelAnswer, snapshot.RubricNotes));
    }

    public static WritingEvaluationDto ToDto(this WritingEvaluation evaluation) => new(evaluation.Status.ToApiValue(),
        evaluation.EstimatedScore, evaluation.Summary, DeserializeStrings(evaluation.StrengthsJson),
        DeserializeStrings(evaluation.ImprovementsJson), evaluation.Feedback.OrderBy(item => item.QuestionPosition)
            .Select(item => new WritingEvaluationFeedbackDto(item.QuestionPosition, item.CriterionScore, item.Feedback,
                item.SuggestedRevision)).ToArray(), evaluation.Provider, evaluation.Model, evaluation.RubricVersion, evaluation.ConsentAcceptedAtUtc,
        evaluation.QueuedAtUtc, evaluation.CompletedAtUtc, evaluation.RetryCount, evaluation.FailureMessage);

    public static WritingAttemptDto ToDto(this WritingAttempt attempt, DateTimeOffset now,
        WritingEvaluation? evaluation = null) => new(attempt.Id,
        attempt.WritingTestId, attempt.TestCode, attempt.TestTitle, attempt.Mode.ToApiValue(),
        attempt.Status.ToApiValue(), attempt.Status == WritingAttemptStatus.InProgress
            ? attempt.CurrentQuestionIndex + 1
            : null, attempt.Status == WritingAttemptStatus.InProgress ? attempt.CurrentPhase.ToApiValue() : null,
        attempt.Status == WritingAttemptStatus.InProgress ? attempt.PhaseEndsAtUtc : null, now, attempt.CreatedAtUtc,
        attempt.SubmittedAtUtc, attempt.Questions.OrderBy(question => question.Position).Select(ToDto).ToArray(),
        evaluation?.ToDto(), evaluation?.Status == WritingEvaluationStatus.Completed
            ? attempt.Questions.OrderBy(question => question.Position).Select(ToReviewDto)
                .Where(question => !string.IsNullOrWhiteSpace(question.ModelAnswer)).ToArray()
            : []);

    public static WritingAttemptSummaryDto ToSummaryDto(this WritingAttempt attempt, WritingEvaluation? evaluation = null) => new(attempt.Id,
        attempt.WritingTestId, attempt.TestCode, attempt.TestTitle, attempt.Mode.ToApiValue(),
        attempt.Status.ToApiValue(), attempt.Questions.Count,
        attempt.Status == WritingAttemptStatus.InProgress ? attempt.CurrentQuestionIndex + 1 : null,
        attempt.CreatedAtUtc, attempt.SubmittedAtUtc, attempt.UpdatedAtUtc, evaluation?.Status.ToApiValue(),
        evaluation?.EstimatedScore);

    private static IReadOnlyList<string> DeserializeStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static WritingAttemptReviewQuestionDto ToReviewDto(WritingAttemptQuestion question)
    {
        var snapshot = JsonSerializer.Deserialize<WritingAttemptReviewSnapshot>(question.ReviewSnapshotJson)
            ?? throw new InvalidOperationException("Invalid Writing attempt review snapshot.");
        return new WritingAttemptReviewQuestionDto(question.Position, snapshot.ModelAnswer ?? string.Empty);
    }
}
