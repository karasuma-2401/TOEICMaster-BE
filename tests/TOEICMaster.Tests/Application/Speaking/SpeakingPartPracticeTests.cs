using System.Text.Json;
using FluentValidation;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingPartPracticeTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly SpeakingAttemptOwner Learner = SpeakingAttemptOwner.ForUser(Guid.NewGuid());
    private readonly InMemorySpeaking _store = new();
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Learners_see_only_published_exercises_of_a_part_without_review_material()
    {
        var published = await CreateAsync(3, "SP3-PUB");
        await CreateAsync(3, "SP3-DRAFT", status: "DRAFT");
        await CreateAsync(5, "SP5-PUB");

        var page = await ExercisesAsync(3);

        var exercise = Assert.Single(page.Items);
        Assert.Equal((published.Id, "SP3-PUB", 3, "Công việc", "MEDIUM", 3, 69), (exercise.Id, exercise.Code,
            exercise.Part, exercise.Topic, exercise.Difficulty, exercise.QuestionCount, exercise.DurationSeconds));
        Assert.Equal(2, (await ExercisesAsync(null)).TotalCount);
        var json = JsonSerializer.Serialize(page, SpeakingJson.Options);
        Assert.DoesNotContain("rubric", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelAnswer", json, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<ValidationException>(() => new GetPublishedSpeakingExercisesQueryValidator()
            .ValidateAndThrowAsync(new GetPublishedSpeakingExercisesQuery(6)));
    }

    [Fact]
    public async Task Practising_one_exercise_runs_its_part_without_a_test()
    {
        var exercise = await CreateAsync(4, "SP4-PRACTICE");

        var attempt = await StartAsync(new StartSpeakingAttemptInput(null, null, exercise.Id));

        Assert.Equal(((Guid?)null, (Guid?)exercise.Id, "PART_4", "IN_PROGRESS", "READING"), (attempt.TestId,
            attempt.ExerciseId, attempt.Mode, attempt.Status, attempt.CurrentPhase));
        Assert.Equal(("SP4-PRACTICE", "Luyện Part 4 · Công việc"), (attempt.TestCode, attempt.TestTitle));
        Assert.Equal([1, 2, 3], attempt.Questions.Select(question => question.Position));
        Assert.All(attempt.Questions, question => Assert.Equal(4, question.Part));
        Assert.Equal([null, null, null], attempt.Questions[0].Content.Questions!);

        var history = await new GetSpeakingAttemptsQueryHandler(_store, new InMemorySpeakingEvaluations(_store))
            .Handle(new GetSpeakingAttemptsQuery(Learner), CancellationToken.None);
        var item = Assert.Single(history.Items);
        Assert.Equal((exercise.Id, "Luyện Part 4 · Công việc", 3), (item.ExerciseId!.Value, item.TestTitle,
            item.QuestionCount));
    }

    [Fact]
    public async Task Practice_snapshots_the_exercise_so_later_edits_do_not_change_it()
    {
        var exercise = await CreateAsync(1, "SP1-SNAP");
        var attempt = await StartAsync(new StartSpeakingAttemptInput(null, null, exercise.Id));

        await new UpdateSpeakingExerciseCommandHandler(_store, _clock).Handle(new UpdateSpeakingExerciseCommand(
            Admin, exercise.Id, exercise.Version, SpeakingSamples.Exercise(1, "SP1-SNAP") with
            {
                Passage = "A completely different passage."
            }), CancellationToken.None);

        var reloaded = await new GetSpeakingAttemptQueryHandler(_store, new InMemorySpeakingEvaluations(_store), _clock)
            .Handle(new GetSpeakingAttemptQuery(Learner, attempt.Id), CancellationToken.None);
        Assert.Equal("Attention, shoppers. The store closes in thirty minutes.", reloaded.Questions[0].Content.Passage);
    }

    [Fact]
    public async Task Only_published_exercises_can_be_practised()
    {
        var draft = await CreateAsync(5, "SP5-DRAFT", status: "DRAFT");

        await Assert.ThrowsAsync<SpeakingNotFoundException>(() =>
            StartAsync(new StartSpeakingAttemptInput(null, null, draft.Id)));
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() => new GetPublishedSpeakingExerciseQueryHandler(_store)
            .Handle(new GetPublishedSpeakingExerciseQuery(draft.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            StartAsync(new StartSpeakingAttemptInput(Guid.NewGuid(), "FULL", draft.Id)));
        await Assert.ThrowsAsync<ValidationException>(() => StartAsync(new StartSpeakingAttemptInput(null, null)));
    }

    [Fact]
    public async Task A_part_two_practice_picture_is_served_while_its_exercise_is_published()
    {
        var image = await new UploadSpeakingImageCommandHandler(_store, _store, _clock).Handle(
            new UploadSpeakingImageCommand(Admin, SpeakingSamples.Png, "image/png"), CancellationToken.None);
        var exercise = await CreateAsync(2, "SP2-IMAGE", image.Id);
        var handler = new GetPublishedSpeakingImageQueryHandler(_store, _store);

        Assert.Equal("image/png", (await handler.Handle(new GetPublishedSpeakingImageQuery(image.Id),
            CancellationToken.None)).ContentType);

        await new ArchiveSpeakingExerciseCommandHandler(_store, _clock).Handle(
            new ArchiveSpeakingExerciseCommand(Admin, exercise.Id, exercise.Version), CancellationToken.None);
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() =>
            handler.Handle(new GetPublishedSpeakingImageQuery(image.Id), CancellationToken.None));
    }

    private Task<SpeakingExerciseDto> CreateAsync(int part, string code, Guid? imageAssetId = null,
        string status = "PUBLISHED") =>
        new CreateSpeakingExerciseCommandHandler(_store, _clock).Handle(new CreateSpeakingExerciseCommand(Admin,
            SpeakingSamples.Exercise(part, code, imageAssetId, status)), CancellationToken.None);

    private Task<SpeakingPageDto<SpeakingPublicExerciseDto>> ExercisesAsync(int? part) =>
        new GetPublishedSpeakingExercisesQueryHandler(_store).Handle(new GetPublishedSpeakingExercisesQuery(part),
            CancellationToken.None);

    private async Task<SpeakingAttemptDto> StartAsync(StartSpeakingAttemptInput input)
    {
        var command = new StartSpeakingAttemptCommand(Learner, input);
        await new StartSpeakingAttemptCommandValidator().ValidateAndThrowAsync(command);
        return await new StartSpeakingAttemptCommandHandler(_store, _clock).Handle(command, CancellationToken.None);
    }
}
