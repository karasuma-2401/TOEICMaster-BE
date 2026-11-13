using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

/// <summary>Phases of a Listening and Reading attempt. Listening locks once Reading starts.</summary>
public static class ExamAttemptPhases
{
    public const string Listening = "listening";
    public const string Reading = "reading";
}

/// <summary>Per-question outcome shown on the review screen.</summary>
public static class ExamReviewStatuses
{
    public const string Correct = "correct";
    public const string Wrong = "wrong";
    public const string Skipped = "skipped";
}

public enum ExamReviewFilter
{
    All = 0,
    Incorrect = 1,
    Flagged = 2
}

public sealed record SaveAttemptAnswerInput(Guid QuestionId, string? SelectedKey, bool? IsFlagged = null);

public sealed record ToggleQuestionFlagInput(bool IsFlagged);

/// <summary>Data for the instructions screen before the learner starts (or resumes) an attempt.</summary>
public sealed record LrExamOverviewDto(
    Guid ExamId,
    string Code,
    string Title,
    int TotalQuestions,
    int ListeningQuestions,
    int ReadingQuestions,
    int DurationMinutes,
    int MinScaledScore,
    int MaxScaledScore,
    Guid? ActiveAttemptId
);

public sealed record ExamAttemptSheetDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamCode,
    string ExamTitle,
    int VersionNumber,
    string? MasterAudioObjectKey,
    string Phase,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ReadingStartedAtUtc,
    int TimeLimitSeconds,
    int RemainingSeconds,
    int TotalQuestions,
    IReadOnlyList<ExamBlockSheetDto> Blocks
);

public sealed record ExamBlockSheetDto(
    Guid Id,
    int Part,
    int FirstQuestionNumber,
    IReadOnlyList<ExamContextBlockDto> ContextBlocks,
    IReadOnlyList<ExamQuestionSheetDto> Questions
);

public sealed record ExamContextBlockDto(
    Guid Id,
    int Ordinal,
    string? Text,
    Guid? ImageAssetId
);

public sealed record ExamQuestionSheetDto(
    Guid Id,
    int Number,
    string? Stem,
    string? OptionA,
    string? OptionB,
    string? OptionC,
    string? OptionD,
    Guid? ImageAssetId,
    string? UserSelectedKey,
    bool IsFlagged
);

public sealed record PartScoreSummaryDto(
    int Part,
    int TotalQuestions,
    int CorrectCount,
    int IncorrectCount,
    int SkippedCount
);

public sealed record ExamAttemptResultDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamCode,
    string ExamTitle,
    int ListeningRawScore,
    int ReadingRawScore,
    int ListeningScaledScore,
    int ReadingScaledScore,
    int TotalScaledScore,
    int ListeningMaxScaledScore,
    int ReadingMaxScaledScore,
    int MaxScaledScore,
    int TotalCorrect,
    int TotalIncorrect,
    int TotalSkipped,
    bool IsTimeExpired,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset SubmittedAtUtc,
    int DurationSeconds,
    IReadOnlyList<PartScoreSummaryDto> Parts
);

public sealed record ExamAttemptReviewCountsDto(int Correct, int Wrong, int Skipped, int Flagged);

public sealed record ExamAttemptReviewDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamCode,
    string ExamTitle,
    int TotalScaledScore,
    int ListeningScaledScore,
    int ReadingScaledScore,
    bool IsTimeExpired,
    ExamAttemptReviewCountsDto Counts,
    IReadOnlyList<ExamAttemptReviewBlockDto> Blocks
);

public sealed record ExamAttemptReviewBlockDto(
    Guid Id,
    int Part,
    int FirstQuestionNumber,
    IReadOnlyList<ExamContextBlockDto> ContextBlocks,
    IReadOnlyList<ExamAttemptReviewQuestionDto> Questions
);

public sealed record ExamAttemptReviewQuestionDto(
    Guid QuestionId,
    int Number,
    int Part,
    string? Stem,
    string? OptionA,
    string? OptionB,
    string? OptionC,
    string? OptionD,
    string? UserSelectedKey,
    string? CorrectKey,
    string Status,
    bool IsFlagged,
    string? Explanation,
    string? Translation,
    Guid? ImageAssetId
);

public sealed record ExamAttemptHistoryItemDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamTitle,
    string ExamCode,
    ExamAttemptStatus Status,
    int? ListeningScaledScore,
    int? ReadingScaledScore,
    int? TotalScaledScore,
    bool IsTimeExpired,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    int? DurationSeconds
);

public sealed record ExamAttemptHistoryPagedDto(
    IReadOnlyList<ExamAttemptHistoryItemDto> Items,
    int TotalCount,
    int PageIndex,
    int PageSize
);
