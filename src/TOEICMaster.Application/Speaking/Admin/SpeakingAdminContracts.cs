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
    IReadOnlyList<SpeakingTestSlotInput?>? Exercises, string? ContentMode = null,
    IReadOnlyList<SpeakingTestQuestionInput?>? Questions = null);

/// <summary>Test-owned content. It deliberately has no code, difficulty or publish status from the exercise bank.</summary>
public sealed record SpeakingTestQuestionContentInput(
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

public sealed record SpeakingTestQuestionInput(int Part, int SequenceNumber,
    SpeakingTestQuestionContentInput? Content);

public sealed record SpeakingTestQuestionContentDto(
    string RubricNotes,
    string ModelAnswer,
    string? Passage = null,
    Guid? ImageAssetId = null,
    string? ImageAlt = null,
    string? Context = null,
    IReadOnlyList<string?>? Questions = null,
    string? DocumentTitle = null,
    IReadOnlyList<SpeakingInfoRowDto>? DocumentRows = null,
    string? CallerIntro = null,
    string? Prompt = null)
{
    public SpeakingContentDto ToContent(int part) => new(part, Passage, ImageAssetId, ImageAlt, Context, Questions,
        DocumentTitle, DocumentRows, CallerIntro, Prompt);
}

public sealed record SpeakingTestFilter(string? Query, string? Status, int PageNumber = 1, int PageSize = 20);

public sealed record SpeakingTestListItemDto(Guid Id, string Code, string Title, string Status, int Version,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc, IReadOnlyDictionary<string, int> PartCounts,
    int QuestionCount, string ContentMode);

public sealed record SpeakingTestSlotDto(Guid ExerciseId, int Part, int SequenceNumber, SpeakingExerciseDto Exercise);

public sealed record SpeakingTestQuestionDto(int Part, int SequenceNumber, SpeakingTestQuestionContentDto Content);

public sealed record SpeakingTestDetailDto(Guid Id, string Code, string Title, string Description, string Status,
    int Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc,
    string ContentMode, IReadOnlyList<SpeakingTestSlotDto> Exercises,
    IReadOnlyList<SpeakingTestQuestionDto> Questions);

/// <summary>A test with its exercise slots, as loaded by repositories.</summary>
public sealed record SpeakingTestWithSlots(SpeakingTest Test, IReadOnlyList<SpeakingTestExercise> Slots);

public sealed partial class SpeakingExerciseInputValidator : AbstractValidator<SpeakingExerciseInput>
{
    public SpeakingExerciseInputValidator()
    {
        RuleFor(input => input.Part).Must(SpeakingTiming.IsPart).WithMessage("Part must be from 1 to 5.");
        RuleFor(input => input.Code).Must(code => code is not null && CodePattern().IsMatch(code.Trim()))
            .WithMessage("Exercise code must be 1-32 alphanumeric or hyphen characters.");
        Text(input => input.Topic, 1, 100, "Topic");
        RuleFor(input => input.Difficulty).Must(value => SpeakingCodes.TryParseDifficulty(value, out _))
            .WithMessage("Invalid difficulty.");
        RuleFor(input => input.Status).Must(value => SpeakingCodes.TryParseExerciseStatus(value, out _))
            .WithMessage("Invalid status.");
        RuleFor(input => input.RubricNotes).Must(value => Length(value) <= 2_000)
            .WithMessage("Rubric notes must not exceed 2000 characters.");
        Text(input => input.ModelAnswer, 1, 4_000, "Model answer");

        When(input => input.Part == 1, () => Text(input => input.Passage, 1, 1_500, "Passage"));
        When(input => input.Part == 2, () =>
        {
            RuleFor(input => input.ImageAssetId).NotNull().WithMessage("Part 2 requires a prompt image.");
            Text(input => input.ImageAlt, 1, 300, "Image description");
        });
        When(input => input.Part == 3, () =>
        {
            Text(input => input.Context, 1, 600, "Context");
            ThreeQuestions();
        });
        When(input => input.Part == 4, () =>
        {
            Text(input => input.DocumentTitle, 1, 200, "Document title");
            RuleFor(input => input.DocumentRows).Must(rows => rows is { Count: >= 1 and <= 20 })
                .WithMessage("Document must have 1 to 20 rows.");
            RuleForEach(input => input.DocumentRows).Must(row => row is not null &&
                    Length(row.Label) is >= 1 and <= 40 && Length(row.Value) is >= 1 and <= 200)
                .WithMessage("Each row requires a 1-40 character label and 1-200 character value.");
            RuleFor(input => input.CallerIntro).Must(value => Length(value) <= 400)
                .WithMessage("Caller intro must not exceed 400 characters.");
            ThreeQuestions();
        });
        When(input => input.Part == 5, () => Text(input => input.Prompt, 1, 600, "Prompt"));
    }

    private void ThreeQuestions()
    {
        RuleFor(input => input.Questions).Must(questions => questions is { Count: 3 })
            .WithMessage("Exactly 3 questions are required.");
        RuleFor(input => input.Questions).Must(SpeakingTestContentRules.HasDistinctQuestions)
            .WithMessage("Questions in an exercise must have distinct content.");
        RuleForEach(input => input.Questions).Must(question => Length(question) is >= 1 and <= 300)
            .WithMessage("Each question must be 1-300 characters.");
    }

    private void Text(Expression<Func<SpeakingExerciseInput, string?>> field, int min,
        int max, string label) =>
        RuleFor(field).Must(value => Length(value) >= min && Length(value) <= max)
            .WithMessage($"{label} must be {min}-{max} characters.");

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
            .WithMessage("Test code must be 1-32 alphanumeric or hyphen characters.");
        RuleFor(input => input.Title).Must(value => SpeakingExerciseInputValidator.Length(value) is >= 1 and <= 200)
            .WithMessage("Test title must be 1-200 characters.");
        RuleFor(input => input.Description).Must(value => SpeakingExerciseInputValidator.Length(value) <= 2_000)
            .WithMessage("Description must not exceed 2000 characters.");
        RuleFor(input => input.ContentMode).Must(mode => string.IsNullOrWhiteSpace(mode) ||
                SpeakingCodes.TryParseTestContentMode(mode, out _))
            .WithMessage("Invalid test content mode.");
        RuleForEach(input => input.Exercises).Must(slot => slot is not null && slot.ExerciseId != Guid.Empty &&
                SpeakingTiming.IsPart(slot.Part) && slot.SequenceNumber >= 1 &&
                slot.SequenceNumber <= SpeakingTiming.SlotsPerPart(slot.Part))
            .When(input => !SpeakingTestInputs.IsDirect(input))
            .WithMessage("Invalid exercise slot.");
        RuleFor(input => input.Exercises).Must(slots => slots is null || slots.Where(slot => slot is not null)
                .Select(slot => slot!.ExerciseId).Distinct().Count() == slots.Count(slot => slot is not null))
            .When(input => !SpeakingTestInputs.IsDirect(input))
            .WithMessage("An exercise cannot appear twice in the same test.");
        RuleFor(input => input.Exercises).Must(slots => slots is null || slots.Where(slot => slot is not null)
                .Select(slot => (slot!.Part, slot.SequenceNumber)).Distinct().Count() ==
                slots.Count(slot => slot is not null))
            .When(input => !SpeakingTestInputs.IsDirect(input))
            .WithMessage("Exercise sequence numbers within each Part must be unique.");
        RuleForEach(input => input.Questions).Must(question => question is not null &&
                SpeakingTiming.IsPart(question.Part) && question.SequenceNumber >= 1 &&
                question.SequenceNumber <= SpeakingTiming.SlotsPerPart(question.Part) && question.Content is not null)
            .When(SpeakingTestInputs.IsDirect)
            .WithMessage("Invalid direct question.");
        RuleFor(input => input.Questions).Must(questions => questions is null || questions.Where(question => question is not null)
                .Select(question => (question!.Part, question.SequenceNumber)).Distinct().Count() ==
                questions.Count(question => question is not null))
            .When(SpeakingTestInputs.IsDirect)
            .WithMessage("Question sequence numbers within each Part must be unique.");
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
            test.UpdatedAtUtc, test.PublishedAtUtc, counts, questionCount, test.ContentMode.ToCode());
    }

    /// <summary>Published slots use their frozen snapshot; draft slots show the current exercise.</summary>
    public static SpeakingTestDetailDto ToDetailDto(this SpeakingTestWithSlots source,
        IReadOnlyDictionary<Guid, SpeakingExercise> currentExercises)
    {
        var slots = source.Slots.Where(slot => slot.ExerciseId is not null).OrderBy(slot => slot.Part)
            .ThenBy(slot => slot.SequenceNumber)
            .Select(slot =>
            {
                var exerciseId = slot.ExerciseId!.Value;
                var exercise = slot.SnapshotJson is not null
                    ? SpeakingJson.Deserialize<SpeakingExerciseDto>(slot.SnapshotJson)
                    : currentExercises.GetValueOrDefault(exerciseId)?.ToDto();
                return exercise is null
                    ? null
                    : new SpeakingTestSlotDto(exerciseId, slot.Part, slot.SequenceNumber, exercise);
            })
            .OfType<SpeakingTestSlotDto>().ToArray();
        var test = source.Test;
        var questions = source.Slots.Where(slot => slot.ContentJson is not null).OrderBy(slot => slot.Part)
            .ThenBy(slot => slot.SequenceNumber).Select(slot => new SpeakingTestQuestionDto(slot.Part,
                slot.SequenceNumber, SpeakingJson.Deserialize<SpeakingTestQuestionContentDto>(slot.ContentJson!)))
            .ToArray();
        return new SpeakingTestDetailDto(test.Id, test.Code, test.Title, test.Description, test.Status.ToCode(),
            test.Version, test.CreatedAtUtc, test.UpdatedAtUtc, test.PublishedAtUtc, test.ContentMode.ToCode(), slots,
            questions);
    }
}
