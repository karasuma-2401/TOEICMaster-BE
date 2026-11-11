using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

public sealed record CreateSpeakingTestCommand(Guid ActorUserId, SpeakingTestInput Input)
    : IRequest<SpeakingTestDetailDto>;

public sealed record UpdateSpeakingTestCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion,
    SpeakingTestInput Input) : IRequest<SpeakingTestDetailDto>;

public sealed record PublishSpeakingTestCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion)
    : IRequest<SpeakingTestDetailDto>;

public sealed record DeleteSpeakingTestDraftCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion) : IRequest;

public sealed class DeleteSpeakingTestDraftCommandValidator : AbstractValidator<DeleteSpeakingTestDraftCommand>
{
    public DeleteSpeakingTestDraftCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class DeleteSpeakingTestDraftCommandHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<DeleteSpeakingTestDraftCommand>
{
    public async Task Handle(DeleteSpeakingTestDraftCommand request, CancellationToken cancellationToken)
    {
        var loaded = await repository.FindTestForUpdateAsync(request.TestId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
        var test = loaded.Test;
        if (test.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Đề đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        if (!test.IsDraft)
            throw new SpeakingConflictException("Chỉ có thể xóa đề nháp. Đề đã công bố không thể xóa.");
        repository.RemoveTest(test);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CreateSpeakingTestCommandHandler(ISpeakingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateSpeakingTestCommand, SpeakingTestDetailDto>
{
    public async Task<SpeakingTestDetailDto> Handle(CreateSpeakingTestCommand request,
        CancellationToken cancellationToken)
    {
        var input = await SpeakingTestInputs.ValidateAsync(request.Input, cancellationToken);
        if (await repository.TestCodeExistsAsync(input.Code!, null, cancellationToken))
            throw new SpeakingConflictException("Mã đề Speaking đã tồn tại.");
        var direct = SpeakingTestInputs.IsDirect(input);
        if (!direct && input.Exercises is null)
            throw SpeakingTestInputs.MissingExercises();
        var exercises = direct
            ? new Dictionary<Guid, SpeakingExercise>()
            : await SpeakingTestInputs.LoadExercisesAsync(input, repository, cancellationToken);
        var test = SpeakingTest.Create(input.Code!, input.Title!, input.Description!,
            direct ? SpeakingTestContentMode.Direct : SpeakingTestContentMode.ExerciseBank, request.ActorUserId,
            timeProvider.GetUtcNow());
        var slots = direct
            ? await SpeakingTestInputs.ToDirectSlotsAsync(test.Id, input, repository, cancellationToken)
            : SpeakingTestInputs.ToSlots(test.Id, input);
        repository.AddTest(test, slots);
        await repository.SaveChangesAsync(cancellationToken);
        return new SpeakingTestWithSlots(test, slots).ToDetailDto(exercises);
    }
}

public sealed class UpdateSpeakingTestCommandHandler(ISpeakingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateSpeakingTestCommand, SpeakingTestDetailDto>
{
    public async Task<SpeakingTestDetailDto> Handle(UpdateSpeakingTestCommand request,
        CancellationToken cancellationToken)
    {
        var input = await SpeakingTestInputs.ValidateAsync(request.Input, cancellationToken);
        var loaded = await repository.FindTestForUpdateAsync(request.TestId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
        var test = loaded.Test;
        if (test.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Đề đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        if (await repository.TestCodeExistsAsync(input.Code!, test.Id, cancellationToken))
            throw new SpeakingConflictException("Mã đề Speaking đã tồn tại.");
        if (!test.IsDraft)
        {
            if (test.ContentMode != SpeakingTestContentMode.ExerciseBank)
                throw new SpeakingConflictException("Đề đã công bố không thể chỉnh sửa.");
            test.UpdateLegacyMetadata(input.Code!, input.Title!, input.Description!, request.ActorUserId,
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return loaded.ToDetailDto(await SpeakingTestInputs.LoadDetailExercisesAsync(loaded, repository,
                cancellationToken));
        }

        var direct = test.ContentMode == SpeakingTestContentMode.Direct;
        if (direct != SpeakingTestInputs.IsDirect(input))
            throw new SpeakingConflictException("Không thể đổi nguồn nội dung của đề Speaking đã tạo.");
        test.UpdateDraft(input.Code!, input.Title!, input.Description!, request.ActorUserId, timeProvider.GetUtcNow());
        if (direct)
        {
            if (input.Questions is null)
            {
                await repository.SaveChangesAsync(cancellationToken);
                return loaded.ToDetailDto(new Dictionary<Guid, SpeakingExercise>());
            }
            var slots = await SpeakingTestInputs.ToDirectSlotsAsync(test.Id, input, repository, cancellationToken);
            await repository.ReplaceDraftSlotsAndSaveAsync(test, slots, cancellationToken);
            return new SpeakingTestWithSlots(test, slots).ToDetailDto(new Dictionary<Guid, SpeakingExercise>());
        }

        if (input.Exercises is null)
        {
            await repository.SaveChangesAsync(cancellationToken);
            return loaded.ToDetailDto(await SpeakingTestInputs.LoadDetailExercisesAsync(loaded, repository,
                cancellationToken));
        }
        var exercises = await SpeakingTestInputs.LoadExercisesAsync(input, repository, cancellationToken);
        var legacySlots = SpeakingTestInputs.ToSlots(test.Id, input);
        await repository.ReplaceDraftSlotsAndSaveAsync(test, legacySlots, cancellationToken);
        return new SpeakingTestWithSlots(test, legacySlots).ToDetailDto(exercises);
    }
}

public sealed class PublishSpeakingTestCommandHandler(ISpeakingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<PublishSpeakingTestCommand, SpeakingTestDetailDto>
{
    public async Task<SpeakingTestDetailDto> Handle(PublishSpeakingTestCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await repository.FindTestForUpdateAsync(request.TestId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
        var test = loaded.Test;
        if (test.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Đề đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        if (!test.IsDraft)
            throw new SpeakingConflictException("Đề Speaking này đã được công bố.");

        if (test.ContentMode == SpeakingTestContentMode.Direct)
        {
            var directErrors = await PublishDirectRequirementsAsync(test, loaded.Slots, repository,
                cancellationToken);
            if (directErrors.Count > 0)
                throw new SpeakingPublishRequirementsException(directErrors);
            test.Publish(request.ActorUserId, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return loaded.ToDetailDto(new Dictionary<Guid, SpeakingExercise>());
        }

        var exercises = (await repository.FindExercisesAsync(loaded.Slots.Select(slot => slot.ExerciseId!.Value)
                .Distinct().ToArray(), cancellationToken)).ToDictionary(exercise => exercise.Id);
        var errors = PublishRequirements(test, loaded.Slots, exercises);
        if (errors.Count > 0)
            throw new SpeakingPublishRequirementsException(errors);

        foreach (var slot in loaded.Slots)
            slot.Freeze(SpeakingJson.Serialize(exercises[slot.ExerciseId!.Value].ToDto()));
        test.Publish(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return loaded.ToDetailDto(exercises);
    }

    /// <summary>Checks every publishing condition and reports each failure separately.</summary>
    internal static Dictionary<string, string[]> PublishRequirements(SpeakingTest test,
        IReadOnlyList<SpeakingTestExercise> slots, IReadOnlyDictionary<Guid, SpeakingExercise> exercises)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(test.Code))
            errors["Code"] = ["Đề cần có mã."];
        if (string.IsNullOrWhiteSpace(test.Title))
            errors["Title"] = ["Đề cần có tên."];
        for (var part = SpeakingTiming.MinPart; part <= SpeakingTiming.MaxPart; part++)
        {
            var expected = SpeakingTiming.SlotsPerPart(part);
            var sequence = slots.Where(slot => slot.Part == part).Select(slot => slot.SequenceNumber).Order().ToArray();
            if (!sequence.SequenceEqual(Enumerable.Range(1, expected)))
                errors[$"Part{part}"] = [$"Part {part} cần đúng {expected} bài, đánh số liên tục từ 1."];
        }

        var exerciseErrors = new List<string>();
        if (slots.Select(slot => slot.ExerciseId).Distinct().Count() != slots.Count)
            exerciseErrors.Add("Một bài tập không được lặp lại trong cùng một đề.");
        foreach (var slot in slots.OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber))
        {
            var label = $"Part {slot.Part} · bài {slot.SequenceNumber}";
            if (slot.ExerciseId is not { } exerciseId || !exercises.TryGetValue(exerciseId, out var exercise))
                exerciseErrors.Add($"{label}: bài tập không tồn tại.");
            else if (exercise.Part != slot.Part)
                exerciseErrors.Add($"{label}: bài tập {exercise.Code} thuộc Part {exercise.Part}.");
            else if (exercise.Status != SpeakingExerciseStatus.Published)
                exerciseErrors.Add($"{label}: bài tập {exercise.Code} chưa được công bố.");
        }
        if (exerciseErrors.Count > 0)
            errors["Exercises"] = exerciseErrors.ToArray();
        return errors;
    }

    internal static Dictionary<string, string[]> PublishDirectRequirements(SpeakingTest test,
        IReadOnlyList<SpeakingTestExercise> slots)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(test.Code))
            errors["Code"] = ["Đề cần có mã."];
        if (string.IsNullOrWhiteSpace(test.Title))
            errors["Title"] = ["Đề cần có tên."];
        for (var part = SpeakingTiming.MinPart; part <= SpeakingTiming.MaxPart; part++)
        {
            var expected = SpeakingTiming.SlotsPerPart(part);
            var sequence = slots.Where(slot => slot.Part == part && slot.ContentJson is not null)
                .Select(slot => slot.SequenceNumber).Order().ToArray();
            if (!sequence.SequenceEqual(Enumerable.Range(1, expected)))
                errors[$"Part{part}"] = [$"Part {part} cần đúng {expected} nội dung, đánh số liên tục từ 1."];
        }
        if (slots.Any(slot => slot.ExerciseId is not null || slot.ContentJson is null))
            errors["Questions"] = ["Đề trực tiếp chỉ được chứa nội dung thuộc chính đề."];
        return errors;
    }

    private static async Task<Dictionary<string, string[]>> PublishDirectRequirementsAsync(SpeakingTest test,
        IReadOnlyList<SpeakingTestExercise> slots, ISpeakingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        var errors = PublishDirectRequirements(test, slots);
        foreach (var slot in slots.Where(slot => slot.ContentJson is not null))
        {
            var key = $"Part{slot.Part}";
            try
            {
                var content = SpeakingJson.Deserialize<SpeakingTestQuestionContentDto>(slot.ContentJson!);
                var exercise = await SpeakingExerciseInputs.ValidateAsync(
                    SpeakingTestInputs.ToDirectExerciseInput(slot.Part, content), cancellationToken);
                await SpeakingExerciseInputs.EnsureImageExistsAsync(exercise, repository, cancellationToken);
            }
            catch (ValidationException exception)
            {
                AddErrors(errors, key, exception.Errors.Select(error => error.ErrorMessage));
            }
            catch (JsonException)
            {
                AddErrors(errors, key, ["Nội dung bản nháp không đọc được."]);
            }
        }
        return errors;
    }

    private static void AddErrors(Dictionary<string, string[]> errors, string key, IEnumerable<string> values) =>
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, .. values]
            : values.ToArray();
}

internal static class SpeakingTestInputs
{
    private static readonly SpeakingTestInputValidator Validator = new();

    public static async Task<SpeakingTestInput> ValidateAsync(SpeakingTestInput input,
        CancellationToken cancellationToken)
    {
        await Validator.ValidateAndThrowAsync(input, cancellationToken);
        return input with
        {
            Code = input.Code!.Trim(),
            Title = input.Title!.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            Exercises = input.Exercises?.OfType<SpeakingTestSlotInput>().ToArray(),
            ContentMode = input.ContentMode?.Trim().ToUpperInvariant(),
            Questions = input.Questions?.OfType<SpeakingTestQuestionInput>().ToArray()
        };
    }

    public static bool IsDirect(SpeakingTestInput input) =>
        SpeakingCodes.TryParseTestContentMode(input.ContentMode, out var mode) &&
        mode == SpeakingTestContentMode.Direct;

    public static ValidationException MissingExercises() => new([
        new ValidationFailure("Exercises", "Danh sách bài tập là bắt buộc.")
    ]);

    /// <summary>Draft slots may be incomplete, but each chosen exercise must exist in the matching Part.</summary>
    public static async Task<IReadOnlyDictionary<Guid, SpeakingExercise>> LoadExercisesAsync(SpeakingTestInput input,
        ISpeakingAdminRepository repository, CancellationToken cancellationToken)
    {
        var slots = input.Exercises!.OfType<SpeakingTestSlotInput>().ToArray();
        var exercises = (await repository.FindExercisesAsync(slots.Select(slot => slot.ExerciseId).ToArray(),
            cancellationToken)).ToDictionary(exercise => exercise.Id);
        var failures = slots.Where(slot => !exercises.TryGetValue(slot.ExerciseId, out var exercise) ||
                                           exercise.Part != slot.Part)
            .Select(slot => new ValidationFailure("Exercises",
                $"Part {slot.Part} · bài {slot.SequenceNumber}: bài tập không tồn tại hoặc không thuộc Part {slot.Part}."))
            .ToArray();
        if (failures.Length > 0)
            throw new ValidationException(failures);
        return exercises;
    }

    public static IReadOnlyList<SpeakingTestExercise> ToSlots(Guid testId, SpeakingTestInput input) =>
        input.Exercises!.OfType<SpeakingTestSlotInput>().OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber)
            .Select(slot => SpeakingTestExercise.Create(testId, slot.Part, slot.SequenceNumber, slot.ExerciseId))
            .ToArray();

    public static async Task<IReadOnlyList<SpeakingTestExercise>> ToDirectSlotsAsync(Guid testId,
        SpeakingTestInput input, ISpeakingAdminRepository repository, CancellationToken cancellationToken)
    {
        var slots = new List<SpeakingTestExercise>();
        foreach (var question in input.Questions ?? [])
        {
            var content = await NormalizeDirectContentAsync(question!.Part, question.Content!, repository,
                cancellationToken);
            slots.Add(SpeakingTestExercise.CreateDirect(testId, question.Part, question.SequenceNumber,
                SpeakingJson.Serialize(content), content.ImageAssetId));
        }
        return slots.OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber).ToArray();
    }

    public static async Task<IReadOnlyDictionary<Guid, SpeakingExercise>> LoadDetailExercisesAsync(
        SpeakingTestWithSlots loaded, ISpeakingAdminRepository repository, CancellationToken cancellationToken)
    {
        var ids = loaded.Slots.Where(slot => slot.SnapshotJson is null && slot.ExerciseId is not null)
            .Select(slot => slot.ExerciseId!.Value).Distinct().ToArray();
        return ids.Length == 0
            ? new Dictionary<Guid, SpeakingExercise>()
            : (await repository.FindExercisesAsync(ids, cancellationToken)).ToDictionary(exercise => exercise.Id);
    }

    private static async Task<SpeakingTestQuestionContentDto> NormalizeDirectContentAsync(int part,
        SpeakingTestQuestionContentInput content, ISpeakingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        ValidateDirectDraftContent(part, content);
        if (part == 2 && content.ImageAssetId is not null)
            await SpeakingExerciseInputs.EnsureImageExistsAsync(ToDirectExerciseInput(part, content), repository,
                cancellationToken);
        return new SpeakingTestQuestionContentDto(content.RubricNotes?.Trim() ?? string.Empty,
            content.ModelAnswer?.Trim() ?? string.Empty, content.Passage?.Trim(),
            part == 2 ? content.ImageAssetId : null, content.ImageAlt?.Trim(), content.Context?.Trim(),
            content.Questions?.Select(question => question?.Trim()).ToArray(), content.DocumentTitle?.Trim(),
            content.DocumentRows?.Select(row => new SpeakingInfoRowDto(row?.Label?.Trim() ?? string.Empty,
                row?.Value?.Trim() ?? string.Empty)).ToArray(), content.CallerIntro?.Trim(), content.Prompt?.Trim());
    }

    internal static SpeakingExerciseInput ToDirectExerciseInput(int part, SpeakingTestQuestionContentDto content) =>
        new(part, "DIRECT", "Nội dung đề", "MEDIUM", "DRAFT", content.RubricNotes, content.ModelAnswer,
            content.Passage, content.ImageAssetId, content.ImageAlt, content.Context, content.Questions,
            content.DocumentTitle, content.DocumentRows?.Select(row => new SpeakingInfoRowInput(row.Label,
                row.Value)).ToArray(), content.CallerIntro, content.Prompt);

    private static SpeakingExerciseInput ToDirectExerciseInput(int part, SpeakingTestQuestionContentInput content) =>
        new(part, "DIRECT", "Nội dung đề", "MEDIUM", "DRAFT", content.RubricNotes, content.ModelAnswer,
            content.Passage, content.ImageAssetId, content.ImageAlt, content.Context, content.Questions,
            content.DocumentTitle, content.DocumentRows, content.CallerIntro, content.Prompt);

    private static void ValidateDirectDraftContent(int part, SpeakingTestQuestionContentInput content)
    {
        var errors = new List<ValidationFailure>();
        AddLimit(errors, "RubricNotes", content.RubricNotes, 2_000);
        AddLimit(errors, "ModelAnswer", content.ModelAnswer, 4_000);
        switch (part)
        {
            case 1:
                AddLimit(errors, "Passage", content.Passage, 1_500);
                break;
            case 2:
                AddLimit(errors, "ImageAlt", content.ImageAlt, 300);
                break;
            case 3:
                AddLimit(errors, "Context", content.Context, 600);
                AddQuestionLimits(errors, content.Questions);
                break;
            case 4:
                AddLimit(errors, "DocumentTitle", content.DocumentTitle, 200);
                AddLimit(errors, "CallerIntro", content.CallerIntro, 400);
                if (content.DocumentRows?.Count > 20)
                    errors.Add(new ValidationFailure("DocumentRows", "Tài liệu tối đa 20 dòng."));
                foreach (var row in content.DocumentRows ?? [])
                {
                    AddLimit(errors, "DocumentRows", row?.Label, 40);
                    AddLimit(errors, "DocumentRows", row?.Value, 200);
                }
                AddQuestionLimits(errors, content.Questions);
                break;
            case 5:
                AddLimit(errors, "Prompt", content.Prompt, 600);
                break;
        }
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static void AddQuestionLimits(List<ValidationFailure> errors, IReadOnlyList<string?>? questions)
    {
        if (questions?.Count > 3)
            errors.Add(new ValidationFailure("Questions", "Tối đa 3 câu hỏi."));
        foreach (var question in questions ?? [])
            AddLimit(errors, "Questions", question, 300);
    }

    private static void AddLimit(List<ValidationFailure> errors, string field, string? value, int maximum)
    {
        if (value?.Trim().Length > maximum)
            errors.Add(new ValidationFailure(field, $"{field} tối đa {maximum} ký tự."));
    }
}
