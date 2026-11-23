
namespace TOEICMaster.Application.ExerciseBank;

// Exercise list displayed in the library.
public sealed record LrPracticeItemDto(
    Guid Id,
    int Part,
    string Kind,
    string Code,
    string? Topic,
    string Difficulty,
    string? Context,
    string? AudioUrl,
    string? ImageUrl,
    int QuestionCount
);

// Exercise detail before starting practice.
// Intentionally omits CorrectKey, Translation, and Explanation.
public sealed record LrPracticeDetailDto(
    Guid Id,
    int Part,
    string Kind,
    string Code,
    string? Topic,
    string Difficulty,
    string? Context,
    string? AudioUrl,
    string? ImageUrl,
    IReadOnlyList<LrPracticeQuestionDto> Questions
);

public sealed record LrPracticeQuestionDto(
    Guid Id,
    int Ordinal,
    string? Code,
    string? Prompt,
    string? OptionA,
    string? OptionB,
    string? OptionC,
    string? OptionD
);

// Payload submitted by the frontend on submission.
public sealed record LrPracticeSubmitRequest(
    IReadOnlyList<LrPracticeAnswerInput> Answers
);

public sealed record LrPracticeAnswerInput(
    Guid QuestionId,
    string? SelectedKey
);

// Result returned after submission.
public sealed record LrPracticeSubmitResultDto(
    Guid ItemId,
    int TotalQuestions,
    int CorrectCount,
    int IncorrectCount,
    IReadOnlyList<LrPracticeQuestionResultDto> Questions
);

public sealed record LrPracticeQuestionResultDto(
    Guid QuestionId,
    int Ordinal,
    string? SelectedKey,
    string CorrectKey,
    bool IsCorrect,
    string? Translation,
    string? Explanation
);
