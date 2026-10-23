using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record WritingExerciseInput(
    int Part,
    string Code,
    string Topic,
    string Difficulty,
    string Status,
    string? RubricNotes,
    string? ModelAnswer,
    Guid? ImageAssetId,
    string? ImageAlt,
    string? KeywordOne,
    string? KeywordTwo,
    string? Sender,
    string? Recipient,
    string? Subject,
    string? EmailBody,
    string? Directions,
    string? Prompt,
    int? MinimumWordCount,
    string? Outline);

public sealed record WritingExerciseDto(
    Guid Id,
    int Part,
    string Code,
    string Topic,
    string Difficulty,
    string Status,
    string RubricNotes,
    string ModelAnswer,
    Guid? ImageAssetId,
    string? ImageAlt,
    string? KeywordOne,
    string? KeywordTwo,
    string? Sender,
    string? Recipient,
    string? Subject,
    string? EmailBody,
    string? Directions,
    string? Prompt,
    int? MinimumWordCount,
    string? Outline,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WritingExerciseFilter(
    int? Part,
    string? Query,
    string? Difficulty,
    string? Status,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record WritingExercisePageDto(
    IReadOnlyList<WritingExerciseDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record WritingMediaAssetDto(Guid Id, string OriginalFileName, string ContentType, long ByteLength,
    DateTimeOffset UploadedAtUtc, string DownloadUrl);

public sealed record WritingTestQuestionInput(Guid ExerciseId, int Part, int SequenceNumber);

public sealed record WritingTestInput(string Code, string Title, string? Description,
    IReadOnlyList<WritingTestQuestionInput> Questions);

public sealed record WritingTestQuestionDto(Guid Id, Guid ExerciseId, int Part, int SequenceNumber,
    string? SnapshotJson);

public sealed record WritingTestDto(Guid Id, string Code, string Title, string Description, string Status,
    int Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<WritingTestQuestionDto> Questions);

public sealed record WritingTestFilter(string? Query, string? Status, int PageNumber = 1, int PageSize = 20);

public sealed record WritingTestPageDto(IReadOnlyList<WritingTestDto> Items, int PageNumber, int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record WritingExerciseSnapshot(
    Guid ExerciseId,
    int Part,
    string Code,
    string Topic,
    string Difficulty,
    string RubricNotes,
    string ModelAnswer,
    Guid? ImageAssetId,
    string? ImageAlt,
    string? KeywordOne,
    string? KeywordTwo,
    string? Sender,
    string? Recipient,
    string? Subject,
    string? EmailBody,
    string? Directions,
    string? Prompt,
    int? MinimumWordCount,
    string? Outline);

public static class WritingAdminMapping
{
    public static WritingPart ParsePart(int part) => part switch
    {
        1 => WritingPart.Part1,
        2 => WritingPart.Part2,
        3 => WritingPart.Part3,
        _ => throw new ArgumentOutOfRangeException(nameof(part))
    };

    public static WritingDifficulty ParseDifficulty(string value) => value.ToUpperInvariant() switch
    {
        "EASY" => WritingDifficulty.Easy,
        "MEDIUM" => WritingDifficulty.Medium,
        "HARD" => WritingDifficulty.Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static WritingExerciseStatus ParseExerciseStatus(string value) => value.ToUpperInvariant() switch
    {
        "DRAFT" => WritingExerciseStatus.Draft,
        "PUBLISHED" => WritingExerciseStatus.Published,
        "ARCHIVED" => WritingExerciseStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static WritingTestStatus ParseTestStatus(string value) => value.ToUpperInvariant() switch
    {
        "DRAFT" => WritingTestStatus.Draft,
        "PUBLISHED" => WritingTestStatus.Published,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this WritingDifficulty value) => value switch
    {
        WritingDifficulty.Easy => "EASY",
        WritingDifficulty.Medium => "MEDIUM",
        WritingDifficulty.Hard => "HARD",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this WritingExerciseStatus value) => value switch
    {
        WritingExerciseStatus.Draft => "DRAFT",
        WritingExerciseStatus.Published => "PUBLISHED",
        WritingExerciseStatus.Archived => "ARCHIVED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this WritingTestStatus value) => value switch
    {
        WritingTestStatus.Draft => "DRAFT",
        WritingTestStatus.Published => "PUBLISHED",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static WritingExerciseDto ToDto(this WritingExercise exercise) => new(
        exercise.Id, (int)exercise.Part, exercise.Code, exercise.Topic, exercise.Difficulty.ToApiValue(),
        exercise.Status.ToApiValue(), exercise.RubricNotes, exercise.ModelAnswer, exercise.ImageAssetId,
        exercise.ImageAlt, exercise.KeywordOne, exercise.KeywordTwo, exercise.Sender, exercise.Recipient,
        exercise.Subject, exercise.EmailBody, exercise.Directions, exercise.Prompt, exercise.MinimumWordCount,
        exercise.Outline, exercise.Version, exercise.CreatedAtUtc, exercise.UpdatedAtUtc);

    public static WritingTestDto ToDto(this WritingTest test) => new(
        test.Id, test.Code, test.Title, test.Description, test.Status.ToApiValue(), test.Version,
        test.CreatedAtUtc, test.UpdatedAtUtc, test.PublishedAtUtc,
        test.Questions.OrderBy(question => question.Part).ThenBy(question => question.SequenceNumber)
            .Select(question => new WritingTestQuestionDto(question.Id, question.ExerciseId, (int)question.Part,
                question.SequenceNumber, question.SnapshotJson)).ToArray());

    public static WritingExerciseSnapshot ToSnapshot(this WritingExercise exercise) => new(
        exercise.Id, (int)exercise.Part, exercise.Code, exercise.Topic, exercise.Difficulty.ToApiValue(),
        exercise.RubricNotes, exercise.ModelAnswer, exercise.ImageAssetId, exercise.ImageAlt, exercise.KeywordOne,
        exercise.KeywordTwo, exercise.Sender, exercise.Recipient, exercise.Subject, exercise.EmailBody,
        exercise.Directions, exercise.Prompt, exercise.MinimumWordCount, exercise.Outline);
}
