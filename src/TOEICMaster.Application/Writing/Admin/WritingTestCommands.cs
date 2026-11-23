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

public sealed record DeleteWritingTestDraftCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion) : IRequest;
public sealed record ArchiveWritingTestCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion) : IRequest<WritingTestDto>;
public sealed record RestoreWritingTestCommand(Guid ActorUserId, Guid TestId, int ExpectedVersion) : IRequest<WritingTestDto>;

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
        RuleForEach(input => input.Questions).NotNull().SetValidator(new WritingTestQuestionInputValidator());
        RuleFor(input => input.Questions).Must(questions => questions.Where(question => question.ExerciseId.HasValue)
            .Select(question => question.ExerciseId).Distinct().Count() == questions.Count(question => question.ExerciseId.HasValue))
            .WithMessage("An exercise cannot appear twice in the same test.")
            .When(input => input.Questions is not null && input.Questions.All(question => question is not null));
        RuleFor(input => input.Questions).Must(questions => questions.Select(question => (question.Part, question.SequenceNumber)).Distinct().Count() == questions.Count)
            .WithMessage("Question sequence numbers within each Part must not be duplicated.")
            .When(input => input.Questions is not null && input.Questions.All(question => question is not null));
    }
}

public sealed class WritingTestQuestionInputValidator : AbstractValidator<WritingTestQuestionInput>
{
    public WritingTestQuestionInputValidator()
    {
        RuleFor(question => question).Must(question => question.ExerciseId.HasValue != (question.Content is not null))
            .WithMessage("Select an exercise or enter question content inline.");
        RuleFor(question => question.ExerciseId).NotEmpty().When(question => question.ExerciseId.HasValue);
        RuleFor(question => question.Part).InclusiveBetween(1, 3);
        RuleFor(question => question.SequenceNumber).GreaterThan(0);
        RuleFor(question => question.Content).SetValidator(new WritingExerciseInputValidator()!)
            .When(question => question.Content is not null);
        RuleFor(question => question.Content!.Part).Equal(question => question.Part)
            .When(question => question.Content is not null);
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

public sealed class DeleteWritingTestDraftCommandValidator : AbstractValidator<DeleteWritingTestDraftCommand>
{
    public DeleteWritingTestDraftCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ArchiveWritingTestCommandValidator : AbstractValidator<ArchiveWritingTestCommand>
{
    public ArchiveWritingTestCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class RestoreWritingTestCommandValidator : AbstractValidator<RestoreWritingTestCommand>
{
    public RestoreWritingTestCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

internal static class WritingTestMutation
{
    public static async Task<WritingTest> FindAsync(IWritingAdminRepository repository, Guid id, int version,
        CancellationToken cancellationToken)
    {
        var test = await repository.FindTestForUpdateAsync(id, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        if (test.Version != version)
            throw new WritingConflictException("The test was updated by someone else. Please reload the data.");
        return test;
    }
}

public sealed class DeleteWritingTestDraftCommandHandler(IWritingAdminRepository repository)
    : IRequestHandler<DeleteWritingTestDraftCommand>
{
    public async Task Handle(DeleteWritingTestDraftCommand request, CancellationToken cancellationToken)
    {
        var test = await WritingTestMutation.FindAsync(repository, request.TestId, request.ExpectedVersion, cancellationToken);
        if (!test.IsDraft)
            throw new WritingConflictException("Only draft tests can be deleted. Archive published tests instead.");
        repository.RemoveTest(test);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ArchiveWritingTestCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<ArchiveWritingTestCommand, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(ArchiveWritingTestCommand request, CancellationToken cancellationToken)
    {
        var test = await WritingTestMutation.FindAsync(repository, request.TestId, request.ExpectedVersion, cancellationToken);
        if (test.Status != WritingTestStatus.Published)
            throw new WritingConflictException("Only published tests can be archived.");
        test.Archive(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return test.ToDto();
    }
}

public sealed class RestoreWritingTestCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<RestoreWritingTestCommand, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(RestoreWritingTestCommand request, CancellationToken cancellationToken)
    {
        var test = await WritingTestMutation.FindAsync(repository, request.TestId, request.ExpectedVersion, cancellationToken);
        if (test.Status != WritingTestStatus.Archived)
            throw new WritingConflictException("Only archived tests can be restored.");
        test.Restore(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return test.ToDto();
    }
}

public sealed class CreateWritingTestCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateWritingTestCommand, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(CreateWritingTestCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input.Normalize();
        if (await repository.TestCodeExistsAsync(input.Code, null, cancellationToken))
            throw new WritingConflictException("Writing test code already exists.");
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
        var ids = inputs.Where(input => input.ExerciseId.HasValue).Select(input => input.ExerciseId!.Value).Distinct().ToArray();
        var exercises = await repository.FindExercisesAsync(ids, cancellationToken);
        if (exercises.Count != ids.Length)
            throw new WritingConflictException("One or more exercises do not exist.");
        var exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        foreach (var input in inputs.Where(input => input.ExerciseId.HasValue))
        {
            var exercise = exercisesById[input.ExerciseId!.Value];
            if (exercise.Status != WritingExerciseStatus.Published || exercise.Part != WritingAdminMapping.ParsePart(input.Part))
                throw new WritingConflictException("Tests can only include published exercises matching the Part.");
        }
        var questions = new List<WritingTestQuestion>();
        foreach (var input in inputs.OrderBy(input => input.Part).ThenBy(input => input.SequenceNumber))
        {
            var part = WritingAdminMapping.ParsePart(input.Part);
            if (input.ExerciseId is Guid exerciseId)
            {
                questions.Add(WritingTestQuestion.Create(testId, exerciseId, part, input.SequenceNumber));
                continue;
            }
            var content = input.Content!.Normalize();
            await CreateWritingExerciseCommandHandler.EnsureImageExistsAsync(content, repository, cancellationToken);
            var snapshot = content.ToEntity(Guid.Empty, DateTimeOffset.UnixEpoch).ToSnapshot();
            questions.Add(WritingTestQuestion.CreateInline(testId, part, input.SequenceNumber,
                JsonSerializer.Serialize(snapshot), content.ImageAssetId));
        }
        return questions;
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
            throw new WritingConflictException("Published tests cannot be edited.");
        if (test.Version != request.ExpectedVersion)
            throw new WritingConflictException("The test was updated by someone else. Please reload the data.");
        if (await repository.TestCodeExistsAsync(input.Code, test.Id, cancellationToken))
            throw new WritingConflictException("Writing test code already exists.");
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
            throw new WritingConflictException("This Writing test has already been published.");
        if (test.Version != request.ExpectedVersion)
            throw new WritingConflictException("The test was updated by someone else. Please reload the data.");
        ValidateQuestionShape(test.Questions);

        var exerciseIds = test.Questions.Where(question => question.ExerciseId.HasValue)
            .Select(question => question.ExerciseId!.Value).ToArray();
        var exercises = await repository.FindExercisesAsync(exerciseIds, cancellationToken);
        if (exercises.Count != exerciseIds.Length)
            throw new WritingConflictException("One or more exercises in the test no longer exist.");
        var exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        foreach (var question in test.Questions)
        {
            if (question.ExerciseId is not Guid exerciseId)
            {
                if (string.IsNullOrWhiteSpace(question.SnapshotJson))
                    throw new WritingConflictException("A question in the test has no content.");
                continue;
            }
            var exercise = exercisesById[exerciseId];
            if (exercise.Status != WritingExerciseStatus.Published || exercise.Part != question.Part)
                throw new WritingConflictException("The test contains an unpublished exercise or an exercise with a mismatched Part.");
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
                throw new WritingConflictException($"Writing {part} must have exactly {expectedCount} questions in consecutive order.");
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
