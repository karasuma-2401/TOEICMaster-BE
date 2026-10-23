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

public sealed class CreateSpeakingTestCommandHandler(ISpeakingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateSpeakingTestCommand, SpeakingTestDetailDto>
{
    public async Task<SpeakingTestDetailDto> Handle(CreateSpeakingTestCommand request,
        CancellationToken cancellationToken)
    {
        var input = await SpeakingTestInputs.ValidateAsync(request.Input, cancellationToken);
        if (await repository.TestCodeExistsAsync(input.Code!, null, cancellationToken))
            throw new SpeakingConflictException("Mã đề Speaking đã tồn tại.");
        var exercises = await SpeakingTestInputs.LoadExercisesAsync(input, repository, cancellationToken);
        var test = SpeakingTest.Create(input.Code!, input.Title!, input.Description!, request.ActorUserId,
            timeProvider.GetUtcNow());
        var slots = SpeakingTestInputs.ToSlots(test.Id, input);
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
        if (!test.IsDraft)
            throw new SpeakingConflictException("Đề đã công bố không thể chỉnh sửa.");
        if (test.Version != request.ExpectedVersion)
            throw new SpeakingConflictException("Đề đã bị thay đổi ở nơi khác. Hãy tải lại dữ liệu.");
        if (await repository.TestCodeExistsAsync(input.Code!, test.Id, cancellationToken))
            throw new SpeakingConflictException("Mã đề Speaking đã tồn tại.");
        var exercises = await SpeakingTestInputs.LoadExercisesAsync(input, repository, cancellationToken);
        test.UpdateDraft(input.Code!, input.Title!, input.Description!, request.ActorUserId, timeProvider.GetUtcNow());
        var slots = SpeakingTestInputs.ToSlots(test.Id, input);
        await repository.ReplaceDraftSlotsAndSaveAsync(test, slots, cancellationToken);
        return new SpeakingTestWithSlots(test, slots).ToDetailDto(exercises);
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

        var exercises = (await repository.FindExercisesAsync(
                loaded.Slots.Select(slot => slot.ExerciseId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(exercise => exercise.Id);
        var errors = PublishRequirements(test, loaded.Slots, exercises);
        if (errors.Count > 0)
            throw new SpeakingPublishRequirementsException(errors);

        foreach (var slot in loaded.Slots)
            slot.Freeze(SpeakingJson.Serialize(exercises[slot.ExerciseId].ToDto()));
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
            if (!exercises.TryGetValue(slot.ExerciseId, out var exercise))
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
            Exercises = input.Exercises!.OfType<SpeakingTestSlotInput>().ToArray()
        };
    }

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
}
