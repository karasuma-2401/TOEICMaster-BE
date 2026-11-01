namespace TOEICMaster.Application.ExamAttempts;

public sealed record StartExamAttemptInput(Guid ExamId, int? TimeLimitMinutes = 120);

public sealed record SaveAttemptAnswerInput(Guid QuestionId, string? SelectedKey, bool? IsFlagged = null);

public sealed record ToggleQuestionFlagInput(bool IsFlagged);

public sealed record ExamAttemptSheetDto(
    Guid AttemptId,
    Guid ExamId,
    int VersionNumber,
    string? MasterAudioObjectKey,
    DateTimeOffset StartedAtUtc,
    int TimeLimitSeconds,
    int RemainingSeconds,
    IReadOnlyList<ExamBlockSheetDto> Blocks
);

public sealed record ExamBlockSheetDto(
    Guid Id,
    int Part,
    int Ordinal,
    int? AudioStartMs,
    int? AudioEndMs,
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

public sealed record ExamAttemptResultDto(
    Guid AttemptId,
    int ListeningRawScore,
    int ReadingRawScore,
    int ListeningScaledScore,
    int ReadingScaledScore,
    int TotalScaledScore,
    int TotalCorrect,
    int TotalIncorrect,
    int TotalSkipped,
    bool IsTimeExpired,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset SubmittedAtUtc,
    IReadOnlyDictionary<int, PartScoreSummaryDto> PartSummaries
);

public sealed record PartScoreSummaryDto(int Part, int TotalQuestions, int CorrectCount, int IncorrectCount, int SkippedCount);

public sealed record ExamAttemptReviewDto(
    Guid AttemptId,
    int TotalScaledScore,
    int ListeningScaledScore,
    int ReadingScaledScore,
    bool IsTimeExpired,
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
    bool IsCorrect,
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
    TOEICMaster.Domain.Entities.ExamAttemptStatus Status,
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
