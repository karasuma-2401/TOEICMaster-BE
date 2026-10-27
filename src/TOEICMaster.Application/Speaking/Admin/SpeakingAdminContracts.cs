using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

public sealed record SpeakingInfoRowInput(string? Label, string? Value);

/// <summary>Create/update body; Part content fields sit flat next to the common fields.</summary>
public sealed record SpeakingExerciseInput(
    int Part,
    string? Code,
    string? Topic,
    string? Difficulty,
    string? Status,
    string? RubricNotes,
    string? ModelAnswer,
    string? Passage = null,
    Guid? ImageAssetId = null,
    string? ImageAlt = null,
    string? Context = null,
    IReadOnlyList<string?>? Questions = null,
    string? DocumentTitle = null,
    IReadOnlyList<SpeakingInfoRowInput?>? DocumentRows = null,
    string? CallerIntro = null,
    string? Prompt = null);

/// <summary>Exercise as returned to admins and frozen into published test snapshots.</summary>
public sealed record SpeakingExerciseDto(
    Guid Id,
    int Part,
    string Code,
    string Topic,
    string Difficulty,
    string Status,
    string RubricNotes,
    string ModelAnswer,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Passage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ImageAssetId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ImageAlt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string?>? Questions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DocumentTitle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SpeakingInfoRowDto>? DocumentRows = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CallerIntro = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Prompt = null)
{
    public SpeakingContentDto ToContent() => new(Part, Passage, ImageAssetId, ImageAlt, Context, Questions,
        DocumentTitle, DocumentRows, CallerIntro, Prompt);
}

public sealed record SpeakingExerciseFilter(int? Part, string? Query, string? Difficulty, string? Status,
    int PageNumber = 1, int PageSize = 20);

public sealed record SpeakingImageDto(Guid Id, string ContentType, long SizeBytes);

public sealed record SpeakingExerciseCountsDto(int Total, int Published, int Draft, int Archived);

public sealed record SpeakingTestCountsDto(int Total, int Published, int Draft);

public sealed record SpeakingAdminSummaryDto(SpeakingExerciseCountsDto Exercises, SpeakingTestCountsDto Tests);

public sealed record SpeakingTestSlotInput(Guid ExerciseId, int Part, int SequenceNumber);

public sealed record SpeakingTestInput(string? Code, string? Title, string? Description,
    IReadOnlyList<SpeakingTestSlotInput?>? Exercises);

public sealed record SpeakingTestFilter(string? Query, string? Status, int PageNumber = 1, int PageSize = 20);

public sealed record SpeakingTestListItemDto(Guid Id, string Code, string Title, string Status, int Version,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc, IReadOnlyDictionary<string, int> PartCounts,
    int QuestionCount);

public sealed record SpeakingTestSlotDto(Guid ExerciseId, int Part, int SequenceNumber, SpeakingExerciseDto Exercise);

public sealed record SpeakingTestDetailDto(Guid Id, string Code, string Title, string Description, string Status,
    int Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<SpeakingTestSlotDto> Exercises);

/// <summary>A test with its exercise slots, as loaded by repositories.</summary>
public sealed record SpeakingTestWithSlots(SpeakingTest Test, IReadOnlyList<SpeakingTestExercise> Slots);

public sealed partial class SpeakingExerciseInputValidator : AbstractValidator<SpeakingExerciseInput>
{
    public SpeakingExerciseInputValidator()
    {
        RuleFor(input => input.Part).Must(SpeakingTiming.IsPart).WithMessage("Part phải từ 1 đến 5.");
        RuleFor(input => input.Code).Must(code => code is not null && CodePattern().IsMatch(code.Trim()))
            .WithMessage("Mã bài gồm 1–32 ký tự chữ, số hoặc dấu gạch ngang.");
        Text(input => input.Topic, 1, 100, "Chủ đề");
        RuleFor(input => input.Difficulty).Must(value => SpeakingCodes.TryParseDifficulty(value, out _))
            .WithMessage("Độ khó không hợp lệ.");
        RuleFor(input => input.Status).Must(value => SpeakingCodes.TryParseExerciseStatus(value, out _))
            .WithMessage("Trạng thái không hợp lệ.");
        RuleFor(input => input.RubricNotes).Must(value => Length(value) <= 2_000)
            .WithMessage("Tiêu chí chấm tối đa 2000 ký tự.");
        Text(input => input.ModelAnswer, 1, 4_000, "Bài mẫu");

        When(input => input.Part == 1, () => Text(input => input.Passage, 1, 1_500, "Đoạn văn"));
        When(input => input.Part == 2, () =>
        {
            RuleFor(input => input.ImageAssetId).NotNull().WithMessage("Part 2 cần ảnh đề bài.");
            Text(input => input.ImageAlt, 1, 300, "Mô tả ảnh");
        });
        When(input => input.Part == 3, () =>
        {
            Text(input => input.Context, 1, 600, "Bối cảnh");
            ThreeQuestions();
        });
        When(input => input.Part == 4, () =>
        {
            Text(input => input.DocumentTitle, 1, 200, "Tiêu đề tài liệu");
            RuleFor(input => input.DocumentRows).Must(rows => rows is { Count: >= 1 and <= 20 })
                .WithMessage("Tài liệu cần từ 1 đến 20 dòng.");
            RuleForEach(input => input.DocumentRows).Must(row => row is not null &&
                    Length(row.Label) is >= 1 and <= 40 && Length(row.Value) is >= 1 and <= 200)
                .WithMessage("Mỗi dòng cần nhãn 1–40 ký tự và nội dung 1–200 ký tự.");
            RuleFor(input => input.CallerIntro).Must(value => Length(value) <= 400)
                .WithMessage("Lời gọi đầu tối đa 400 ký tự.");
            ThreeQuestions();
        });
        When(input => input.Part == 5, () => Text(input => input.Prompt, 1, 600, "Đề bài"));
    }

    private void ThreeQuestions()
    {
        RuleFor(input => input.Questions).Must(questions => questions is { Count: 3 })
            .WithMessage("Cần đúng 3 câu hỏi.");
        RuleForEach(input => input.Questions).Must(question => Length(question) is >= 1 and <= 300)
            .WithMessage("Mỗi câu hỏi cần 1–300 ký tự.");
    }

    private void Text(Expression<Func<SpeakingExerciseInput, string?>> field, int min,
        int max, string label) =>
        RuleFor(field).Must(value => Length(value) >= min && Length(value) <= max)
            .WithMessage($"{label} cần {min}–{max} ký tự.");

    internal static int Length(string? value) => value?.Trim().Length ?? 0;

    [GeneratedRegex("^[A-Za-z0-9-]{1,32}$")]
    internal static partial Regex CodePattern();
}

public sealed class SpeakingTestInputValidator : AbstractValidator<SpeakingTestInput>
{
    public SpeakingTestInputValidator()
    {
        RuleFor(input => input.Code).Must(code => code is not null &&
                SpeakingExerciseInputValidator.CodePattern().IsMatch(code.Trim()))
            .WithMessage("Mã đề gồm 1–32 ký tự chữ, số hoặc dấu gạch ngang.");
        RuleFor(input => input.Title).Must(value => SpeakingExerciseInputValidator.Length(value) is >= 1 and <= 200)
            .WithMessage("Tên đề cần 1–200 ký tự.");
        RuleFor(input => input.Description).Must(value => SpeakingExerciseInputValidator.Length(value) <= 2_000)
            .WithMessage("Mô tả tối đa 2000 ký tự.");
        RuleFor(input => input.Exercises).NotNull().WithMessage("Danh sách bài tập là bắt buộc.");
        RuleForEach(input => input.Exercises).Must(slot => slot is not null && slot.ExerciseId != Guid.Empty &&
                SpeakingTiming.IsPart(slot.Part) && slot.SequenceNumber >= 1 &&
                slot.SequenceNumber <= SpeakingTiming.SlotsPerPart(slot.Part))
            .WithMessage("Vị trí bài tập không hợp lệ.");
        RuleFor(input => input.Exercises).Must(slots => slots is null || slots.Where(slot => slot is not null)
                .Select(slot => slot!.ExerciseId).Distinct().Count() == slots.Count(slot => slot is not null))
            .WithMessage("Một bài tập không thể xuất hiện hai lần trong cùng một đề.");
        RuleFor(input => input.Exercises).Must(slots => slots is null || slots.Where(slot => slot is not null)
                .Select(slot => (slot!.Part, slot.SequenceNumber)).Distinct().Count() ==
                slots.Count(slot => slot is not null))
            .WithMessage("Thứ tự bài tập trong mỗi Part không được trùng nhau.");
    }
}

public static class SpeakingAdminMapping
{
    public static SpeakingContentDto ReadContent(this SpeakingExercise exercise) =>
        SpeakingJson.Deserialize<SpeakingContentDto>(exercise.ContentJson);

    public static SpeakingExerciseDto ToDto(this SpeakingExercise exercise)
    {
        var content = exercise.ReadContent();
        return new SpeakingExerciseDto(exercise.Id, exercise.Part, exercise.Code, exercise.Topic,
            exercise.Difficulty.ToCode(), exercise.Status.ToCode(), exercise.RubricNotes, exercise.ModelAnswer,
            exercise.Version, exercise.CreatedAtUtc, exercise.UpdatedAtUtc, content.Passage, content.ImageAssetId,
            content.ImageAlt, content.Context, content.Questions, content.DocumentTitle, content.DocumentRows,
            content.CallerIntro, content.Prompt);
    }

    public static SpeakingTestListItemDto ToListItemDto(this SpeakingTestWithSlots source)
    {
        var counts = Enumerable.Range(SpeakingTiming.MinPart, SpeakingTiming.MaxPart)
            .ToDictionary(part => part.ToString(CultureInfo.InvariantCulture),
                part => source.Slots.Count(slot => slot.Part == part));
        var questionCount = source.Slots.Sum(slot => SpeakingTiming.QuestionsPerExercise(slot.Part));
        var test = source.Test;
        return new SpeakingTestListItemDto(test.Id, test.Code, test.Title, test.Status.ToCode(), test.Version,
            test.UpdatedAtUtc, test.PublishedAtUtc, counts, questionCount);
    }

    /// <summary>Published slots use their frozen snapshot; draft slots show the current exercise.</summary>
    public static SpeakingTestDetailDto ToDetailDto(this SpeakingTestWithSlots source,
        IReadOnlyDictionary<Guid, SpeakingExercise> currentExercises)
    {
        var slots = source.Slots.OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber)
            .Select(slot =>
            {
                var exercise = slot.SnapshotJson is not null
                    ? SpeakingJson.Deserialize<SpeakingExerciseDto>(slot.SnapshotJson)
                    : currentExercises.GetValueOrDefault(slot.ExerciseId)?.ToDto();
                return exercise is null
                    ? null
                    : new SpeakingTestSlotDto(slot.ExerciseId, slot.Part, slot.SequenceNumber, exercise);
            })
            .OfType<SpeakingTestSlotDto>().ToArray();
        var test = source.Test;
        return new SpeakingTestDetailDto(test.Id, test.Code, test.Title, test.Description, test.Status.ToCode(),
            test.Version, test.CreatedAtUtc, test.UpdatedAtUtc, test.PublishedAtUtc, slots);
    }
}
