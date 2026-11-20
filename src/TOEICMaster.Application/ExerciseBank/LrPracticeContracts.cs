
namespace TOEICMaster.Application.ExerciseBank;

// Danh sách bài tập hiển thị trong thư viện.
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

// Chi tiết bài tập trước khi làm.
// Cố ý KHÔNG có CorrectKey, Translation, Explanation.
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

// Dữ liệu frontend gửi lên khi nộp bài.
public sealed record LrPracticeSubmitRequest(
    IReadOnlyList<LrPracticeAnswerInput> Answers
);

public sealed record LrPracticeAnswerInput(
    Guid QuestionId,
    string? SelectedKey
);

// Kết quả trả về sau khi nộp bài.
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
