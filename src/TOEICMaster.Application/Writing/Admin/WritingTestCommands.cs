using System.Text.Json;
using FluentValidation;
using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record CreateWritingTestCommand(Guid ActorUserId, WritingTestInput Input) : IRequest<WritingTestDto>;

public sealed record UpdateWritingTestDraftCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion,
    WritingTestInput Input) : IRequest<WritingTestDto>;

public sealed record PublishWritingTestCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion)
    : IRequest<WritingTestDto>;

public sealed class WritingTestInputValidator : AbstractValidator<WritingTestInput>
{
    public WritingTestInputValidator()
    {
        RuleFor(input => input.Code).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(value => value is not null && value.Trim().Length <= 64);
        RuleFor(input => input.Title).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(value => value is not null && value.Trim().Length <= 200);
        RuleFor(input => input.Description).Must(value => value is null || value.Trim().Length <= 2_000);
        RuleFor(input => input.Questions).NotNull();
        RuleForEach(input => input.Questions).SetValidator(new WritingTestQuestionInputValidator());
        RuleFor(input => input.Questions).Must(questions => questions.Select(question => question.ExerciseId).Distinct().Count() == questions.Count)
            .WithMessage("Một bài tập không thể xuất hiện hai lần trong cùng một đề.");
        RuleFor(input => input.Questions).Must(questions => questions.Select(question => (question.Part, question.SequenceNumber)).Distinct().Count() == questions.Count)
            .WithMessage("Thứ tự câu hỏi trong mỗi Part không được trùng nhau.");
    }
}

public sealed class WritingTestQuestionInputValidator : AbstractValidator<WritingTestQuestionInput>
{
    public WritingTestQuestionInputValidator()
    {
        RuleFor(question => question.ExerciseId).NotEmpty();
        RuleFor(question => question.Part).InclusiveBetween(1, 3);
        RuleFor(question => question.SequenceNumber).GreaterThan(0);
    }
}

public sealed class CreateWritingTestCommandValidator : AbstractValidator<CreateWritingTestCommand>
{
    public CreateWritingTestCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Input).SetValidator(new WritingTestInputValidator());
    }
}

public sealed class UpdateWritingTestDraftCommandValidator : AbstractValidator<UpdateWritingTestDraftCommand>
{
    public UpdateWritingTestDraftCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.Input).SetValidator(new WritingTestInputValidator());
    }
}

public sealed class PublishWritingTestCommandValidator : AbstractValidator<PublishWritingTestCommand>
{
    public PublishWritingTestCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class CreateWritingTestCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateWritingTestCommand, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(CreateWritingTestCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input.Normalize();
        if (await repository.TestCodeExistsAsync(input.Code, null, cancellationToken))
            throw new WritingConflictException("Mã đề Writing đã tồn tại.");
        var test = WritingTest.Create(input.Code, input.Title, input.Description ?? string.Empty, request.ActorUserId,
            timeProvider.GetUtcNow());
        var questions = await BuildDraftQuestionsAsync(test.Id, input.Questions, repository, cancellationToken);
        test.SetInitialQuestions(questions);
        await repository.AddTestAsync(test, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return test.ToDto();
    }

    internal static async Task<IReadOnlyList<WritingTestQuestion>> BuildDraftQuestionsAsync(Guid testId,
        IReadOnlyList<WritingTestQuestionInput> inputs, IWritingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        var ids = inputs.Select(input => input.ExerciseId).Distinct().ToArray();
        var exercises = await repository.FindExercisesAsync(ids, cancellationToken);
        if (exercises.Count != ids.Length)
            throw new WritingConflictException("Một hoặc nhiều bài tập không tồn tại.");
        var exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        foreach (var input in inputs)
        {
            var exercise = exercisesById[input.ExerciseId];
            if (exercise.Status != WritingExerciseStatus.Published || exercise.Part != WritingAdminMapping.ParsePart(input.Part))
                throw new WritingConflictException("Đề chỉ có thể chọn bài tập đã công bố và đúng Part.");
        }
        return inputs.OrderBy(input => input.Part).ThenBy(input => input.SequenceNumber)
            .Select(input => WritingTestQuestion.Create(testId, input.ExerciseId,
                WritingAdminMapping.ParsePart(input.Part), input.SequenceNumber)).ToArray();
    }
}

public sealed class UpdateWritingTestDraftCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateWritingTestDraftCommand, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(UpdateWritingTestDraftCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input.Normalize();
        var test = await repository.FindTestForUpdateAsync(request.TestId, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        if (!test.IsDraft)
            throw new WritingConflictException("Đề đã công bố không thể chỉnh sửa.");
        if (test.Version != request.ExpectedVersion)
            throw new WritingConflictException("Đề đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (await repository.TestCodeExistsAsync(input.Code, test.Id, cancellationToken))
            throw new WritingConflictException("Mã đề Writing đã tồn tại.");
        var questions = await CreateWritingTestCommandHandler.BuildDraftQuestionsAsync(test.Id, input.Questions,
            repository, cancellationToken);
        test.UpdateDraft(input.Code, input.Title, input.Description ?? string.Empty, questions, request.ActorUserId,
            timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return test.ToDto();
    }
}

public sealed class PublishWritingTestCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<PublishWritingTestCommand, WritingTestDto>
{
    private static readonly IReadOnlyDictionary<WritingPart, int> RequiredQuestionCounts =
        new Dictionary<WritingPart, int>
        {
            [WritingPart.Part1] = 5,
            [WritingPart.Part2] = 2,
            [WritingPart.Part3] = 1
        };

    public async Task<WritingTestDto> Handle(PublishWritingTestCommand request, CancellationToken cancellationToken)
    {
        var test = await repository.FindTestForUpdateAsync(request.TestId, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        if (!test.IsDraft)
            throw new WritingConflictException("Đề Writing này đã được công bố.");
        if (test.Version != request.ExpectedVersion)
            throw new WritingConflictException("Đề đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        ValidateQuestionShape(test.Questions);

        var exerciseIds = test.Questions.Select(question => question.ExerciseId).ToArray();
        var exercises = await repository.FindExercisesAsync(exerciseIds, cancellationToken);
        if (exercises.Count != exerciseIds.Length)
            throw new WritingConflictException("Một hoặc nhiều bài tập của đề không còn tồn tại.");
        var exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        foreach (var question in test.Questions)
        {
            var exercise = exercisesById[question.ExerciseId];
            if (exercise.Status != WritingExerciseStatus.Published || exercise.Part != question.Part)
                throw new WritingConflictException("Đề có bài tập chưa công bố hoặc không đúng Part.");
            question.Freeze(JsonSerializer.Serialize(exercise.ToSnapshot()));
        }
        test.Publish(timeProvider.GetUtcNow(), request.ActorUserId);
        await repository.SaveChangesAsync(cancellationToken);
        return test.ToDto();
    }

    private static void ValidateQuestionShape(IReadOnlyCollection<WritingTestQuestion> questions)
    {
        foreach (var (part, expectedCount) in RequiredQuestionCounts)
        {
            var selected = questions.Where(question => question.Part == part)
                .OrderBy(question => question.SequenceNumber).ToArray();
            if (selected.Length != expectedCount || selected.Select(question => question.SequenceNumber)
                    .SequenceEqual(Enumerable.Range(1, expectedCount)) is false)
                throw new WritingConflictException($"Writing {part} phải có đúng {expectedCount} câu theo thứ tự liên tục.");
        }
    }
}

internal static class WritingTestInputExtensions
{
    public static WritingTestInput Normalize(this WritingTestInput input) => input with
    {
        Code = input.Code.Trim(),
        Title = input.Title.Trim(),
        Description = input.Description?.Trim() ?? string.Empty,
        Questions = input.Questions.OrderBy(question => question.Part).ThenBy(question => question.SequenceNumber).ToArray()
    };
}
