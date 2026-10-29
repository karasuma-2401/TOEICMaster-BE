using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record LrExerciseQuestionInput(string Code, string Prompt, string OptionA, string OptionB,
    string OptionC, string? OptionD, string CorrectKey, string? Translation, string? Explanation);

public sealed record LrExerciseItemInput(string Code, string Topic, string Difficulty, string Status, string? Context,
    string? AudioUrl, string? ImageUrl, IReadOnlyList<LrExerciseQuestionInput> Questions);

public sealed record LrExerciseQuestionDto(Guid Id, int Ordinal, string Code, string Prompt, string OptionA,
    string OptionB, string OptionC, string? OptionD, string CorrectKey, string? Translation, string? Explanation);

public sealed record LrExerciseItemDto(Guid Id, int Part, string Kind, string Code, string Topic, string Difficulty,
    string Status, string? Context, string? AudioUrl, string? ImageUrl, IReadOnlyList<LrExerciseQuestionDto> Questions,
    int Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record LrExerciseItemFilter(int Part, string? Query, string? Difficulty, string? Status,
    int PageNumber = 1, int PageSize = 20);

public sealed record LrExerciseItemPageDto(IReadOnlyList<LrExerciseItemDto> Items, int PageNumber, int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record LrExercisePartSummaryDto(int Part, string Kind, int ItemCount, int QuestionCount,
    DateTimeOffset? UpdatedAtUtc);

public sealed record LrExerciseBankSummaryDto(int TotalQuestionCount, DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<LrExercisePartSummaryDto> Parts);

public sealed record LrExerciseImportResultDto(int Part, int CreatedItemCount, int CreatedQuestionCount,
    LrExerciseBankSummaryDto Summary);

public static class LrExerciseBankMapping
{
    public static LrExercisePart ParsePart(int value) => Enum.IsDefined((LrExercisePart)value)
        ? (LrExercisePart)value
        : throw new ArgumentOutOfRangeException(nameof(value));

    public static LrExerciseDifficulty ParseDifficulty(string value) => value.Trim().ToUpperInvariant() switch
    {
        "EASY" => LrExerciseDifficulty.Easy,
        "MEDIUM" => LrExerciseDifficulty.Medium,
        "HARD" => LrExerciseDifficulty.Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static LrExerciseStatus ParseStatus(string value) => value.Trim().ToUpperInvariant() switch
    {
        "DRAFT" => LrExerciseStatus.Draft,
        "PUBLISHED" => LrExerciseStatus.Published,
        "ARCHIVED" => LrExerciseStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this LrExerciseDifficulty value) => value.ToString().ToUpperInvariant();
    public static string ToApiValue(this LrExerciseStatus value) => value.ToString().ToUpperInvariant();
    public static string ToApiValue(this LrExerciseItemKind value) => value.ToString().ToUpperInvariant();

    public static LrExerciseBankItem ToEntity(this LrExerciseItemInput input, int part, Guid actorUserId,
        DateTimeOffset now) => LrExerciseBankItem.Create(ParsePart(part), input.Code, input.Topic,
        ParseDifficulty(input.Difficulty), ParseStatus(input.Status), input.Context, input.AudioUrl, input.ImageUrl,
        input.Questions.Select(question => new LrExerciseBankQuestionInput(question.Code, question.Prompt,
            question.OptionA, question.OptionB, question.OptionC, question.OptionD, question.CorrectKey,
            question.Translation, question.Explanation)).ToArray(), actorUserId, now);

    public static LrExerciseItemDto ToDto(this LrExerciseBankItem item) => new(item.Id, (int)item.Part,
        item.Kind.ToApiValue(), item.Code, item.Topic, item.Difficulty.ToApiValue(), item.Status.ToApiValue(),
        item.Context, item.AudioUrl, item.ImageUrl, item.Questions.OrderBy(question => question.Ordinal)
            .Select(question => new LrExerciseQuestionDto(question.Id, question.Ordinal, question.Code,
                question.Prompt, question.OptionA, question.OptionB, question.OptionC, question.OptionD,
                question.CorrectKey, question.Translation, question.Explanation)).ToArray(), item.Version,
        item.CreatedAtUtc, item.UpdatedAtUtc);
}
