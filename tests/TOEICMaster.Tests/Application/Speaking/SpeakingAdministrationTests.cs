using FluentValidation;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingAdministrationTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private readonly InMemorySpeaking _store = new();
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero));

    public static TheoryData<SpeakingExerciseInput, string> InvalidExercises => new()
    {
        { SpeakingSamples.Exercise(1, "SP1-X") with { Passage = " " }, "Passage" },
        { SpeakingSamples.Exercise(1, "SP1-X") with { Passage = new string('a', 1_501) }, "Passage" },
        { SpeakingSamples.Exercise(2, "SP2-X") with { ImageAssetId = null }, "ImageAssetId" },
        { SpeakingSamples.Exercise(2, "SP2-X", Guid.NewGuid()) with { ImageAlt = "" }, "ImageAlt" },
        { SpeakingSamples.Exercise(3, "SP3-X") with { Questions = ["One?", "Two?"] }, "Questions" },
        { SpeakingSamples.Exercise(3, "SP3-X") with { Questions = ["One?", "Two?", " "] }, "Questions[2]" },
        { SpeakingSamples.Exercise(4, "SP4-X") with { DocumentRows = [] }, "DocumentRows" },
        {
            SpeakingSamples.Exercise(4, "SP4-X") with
            {
                DocumentRows = Enumerable.Range(1, 21).Select(i => new SpeakingInfoRowInput($"{i}", "x")).ToArray()
            },
            "DocumentRows"
        },
        { SpeakingSamples.Exercise(4, "SP4-X") with { Questions = null }, "Questions" },
        { SpeakingSamples.Exercise(5, "SP5-X") with { ModelAnswer = "" }, "ModelAnswer" },
        { SpeakingSamples.Exercise(5, "SP5 X"), "Code" },
        { SpeakingSamples.Exercise(5, "SP5-X") with { Difficulty = "EXTREME" }, "Difficulty" },
        { SpeakingSamples.Exercise(5, "SP5-X") with { Part = 6 }, "Part" }
    };

    [Theory]
    [MemberData(nameof(InvalidExercises))]
    public async Task Invalid_exercises_report_the_offending_field_without_a_prefix(SpeakingExerciseInput input,
        string field)
    {
        var handler = new CreateSpeakingExerciseCommandHandler(_store, _clock);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new CreateSpeakingExerciseCommand(Admin, input), CancellationToken.None));

        Assert.Contains(error.Errors, failure => failure.PropertyName == field);
        Assert.Empty(_store.Exercises);
    }

    [Fact]
    public async Task Exercise_content_is_stored_flat_and_only_for_its_part()
    {
        var input = SpeakingSamples.Exercise(1, " sp1-001 ") with { Prompt = "ignored", Questions = ["x", "y", "z"] };

        var created = await new CreateSpeakingExerciseCommandHandler(_store, _clock)
            .Handle(new CreateSpeakingExerciseCommand(Admin, input), CancellationToken.None);

        Assert.Equal(("sp1-001", 1, "PUBLISHED", "MEDIUM", 1), (created.Code, created.Part, created.Status,
            created.Difficulty, created.Version));
        Assert.Equal(input.Passage, created.Passage);
        Assert.Null(created.Prompt);
        Assert.Null(created.Questions);
    }

    [Fact]
    public async Task Duplicate_codes_and_stale_versions_conflict()
    {
        var created = await CreateAsync(SpeakingSamples.Exercise(5, "SP5-001"));

        await Assert.ThrowsAsync<SpeakingConflictException>(() => CreateAsync(SpeakingSamples.Exercise(5, "sp5-001")));
        await Assert.ThrowsAsync<SpeakingConflictException>(() => new UpdateSpeakingExerciseCommandHandler(_store, _clock)
            .Handle(new UpdateSpeakingExerciseCommand(Admin, created.Id, created.Version + 1,
                SpeakingSamples.Exercise(5, "SP5-001")), CancellationToken.None));
        await Assert.ThrowsAsync<SpeakingConflictException>(() => new UpdateSpeakingExerciseCommandHandler(_store, _clock)
            .Handle(new UpdateSpeakingExerciseCommand(Admin, created.Id, created.Version,
                SpeakingSamples.Exercise(1, "SP5-001")), CancellationToken.None));
    }

    [Fact]
    public async Task Part_two_needs_an_uploaded_image_asset()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateAsync(SpeakingSamples.Exercise(2, "SP2-001", Guid.NewGuid())));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "ImageAssetId");
    }

    [Fact]
    public async Task Images_are_checked_by_magic_bytes_and_size()
    {
        var handler = new UploadSpeakingImageCommandHandler(_store, _store, _clock);

        var image = await handler.Handle(new UploadSpeakingImageCommand(Admin, SpeakingSamples.Png, "image/png"),
            CancellationToken.None);
        Assert.Equal(("image/png", SpeakingSamples.Png.Length), (image.ContentType, (int)image.SizeBytes));
        Assert.Equal(64, _store.Assets.Single().Sha256.Length);

        await Assert.ThrowsAsync<InvalidSpeakingMediaException>(() => handler.Handle(
            new UploadSpeakingImageCommand(Admin, "GIF89a not a png"u8.ToArray(), "image/png"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidSpeakingMediaException>(() => handler.Handle(
            new UploadSpeakingImageCommand(Admin, SpeakingSamples.Png, "image/jpeg"), CancellationToken.None));
        await Assert.ThrowsAsync<SpeakingMediaTooLargeException>(() => handler.Handle(
            new UploadSpeakingImageCommand(Admin, new byte[SpeakingMediaRules.MaxImageBytes + 1], "image/png"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Publishing_reports_every_unmet_requirement()
    {
        var draft = await CreateAsync(SpeakingSamples.Exercise(1, "SP1-D", status: "DRAFT"));
        var opinion = await CreateAsync(SpeakingSamples.Exercise(5, "SP5-001"));
        var test = await new CreateSpeakingTestCommandHandler(_store, _clock).Handle(new CreateSpeakingTestCommand(Admin,
            new SpeakingTestInput("SPK-01", "Practice", null,
            [
                new SpeakingTestSlotInput(draft.Id, 1, 2),
                new SpeakingTestSlotInput(opinion.Id, 5, 1)
            ])), CancellationToken.None);

        var error = await Assert.ThrowsAsync<SpeakingPublishRequirementsException>(() => Publish(test.Id, test.Version));

        Assert.Equal(["Exercises", "Part1", "Part2", "Part3", "Part4"], error.Errors.Keys.Order());
        Assert.Contains(error.Errors["Exercises"], message => message.Contains("chưa được công bố"));
        Assert.Equal(SpeakingTestStatus.Draft, _store.Tests.Single().Status);
    }

    [Fact]
    public async Task Publishing_rejects_wrong_parts_duplicates_and_stale_versions()
    {
        var test = await FullTestAsync();
        var slot = _store.Slots.First(item => item.Part == 1 && item.SequenceNumber == 2);
        _store.Slots.Remove(slot);
        _store.Slots.Add(SpeakingTestExercise.Create(test.Id, 1, 2,
            _store.Slots.First(item => item.Part == 5).ExerciseId!.Value));

        await Assert.ThrowsAsync<SpeakingConflictException>(() => Publish(test.Id, test.Version + 1));
        var error = await Assert.ThrowsAsync<SpeakingPublishRequirementsException>(() => Publish(test.Id, test.Version));
        Assert.Contains(error.Errors["Exercises"], message => message.Contains("không được lặp lại"));
        Assert.Contains(error.Errors["Exercises"], message => message.Contains("thuộc Part 5"));
    }

    [Fact]
    public async Task Published_legacy_tests_keep_their_snapshot_and_allow_metadata_edits()
    {
        var test = await FullTestAsync();
        var published = await Publish(test.Id, test.Version);
        Assert.Equal(("PUBLISHED", test.Version + 1), (published.Status, published.Version));
        Assert.NotNull(published.PublishedAtUtc);

        var passage = _store.Exercises.Single(exercise => exercise.Code == "SP1-001");
        await new UpdateSpeakingExerciseCommandHandler(_store, _clock).Handle(new UpdateSpeakingExerciseCommand(Admin,
            passage.Id, passage.Version, SpeakingSamples.Exercise(1, "SP1-001") with { Passage = "Changed." }),
            CancellationToken.None);
        await new ArchiveSpeakingExerciseCommandHandler(_store, _clock).Handle(
            new ArchiveSpeakingExerciseCommand(Admin, passage.Id, passage.Version), CancellationToken.None);

        var detail = await new GetSpeakingTestQueryHandler(_store).Handle(new GetSpeakingTestQuery(test.Id),
            CancellationToken.None);
        var frozen = detail.Exercises.Single(slot => slot.ExerciseId == passage.Id).Exercise;
        Assert.Equal(("Attention, shoppers. The store closes in thirty minutes.", "PUBLISHED"),
            (frozen.Passage, frozen.Status));
        var edited = await new UpdateSpeakingTestCommandHandler(_store, _clock).Handle(
            new UpdateSpeakingTestCommand(Admin, test.Id, published.Version,
                new SpeakingTestInput("SPK-01", "Edited", null, null)), CancellationToken.None);
        Assert.Equal(("Edited", "PUBLISHED", 3), (edited.Title, edited.Status, edited.Version));
    }

    [Fact]
    public async Task Draft_tests_may_be_incomplete_and_can_swap_exercises()
    {
        var first = await CreateAsync(SpeakingSamples.Exercise(1, "SP1-001"));
        var second = await CreateAsync(SpeakingSamples.Exercise(1, "SP1-002"));
        var handler = new UpdateSpeakingTestCommandHandler(_store, _clock);
        var test = await new CreateSpeakingTestCommandHandler(_store, _clock).Handle(new CreateSpeakingTestCommand(Admin,
            new SpeakingTestInput("SPK-02", "Draft", "", [new(first.Id, 1, 1), new(second.Id, 1, 2)])),
            CancellationToken.None);

        var updated = await handler.Handle(new UpdateSpeakingTestCommand(Admin, test.Id, test.Version,
            new SpeakingTestInput("SPK-02", "Draft", "", [new(second.Id, 1, 1), new(first.Id, 1, 2)])),
            CancellationToken.None);

        Assert.Equal([second.Id, first.Id], updated.Exercises.Select(slot => slot.ExerciseId));
        Assert.Equal(2, updated.Version);
        var list = await new GetSpeakingTestsQueryHandler(_store).Handle(
            new GetSpeakingTestsQuery(new SpeakingTestFilter(null, null)), CancellationToken.None);
        Assert.Equal(2, list.Items.Single().PartCounts["1"]);
        Assert.Equal(0, list.Items.Single().PartCounts["5"]);
        Assert.Equal(2, list.Items.Single().QuestionCount);
    }

    [Fact]
    public async Task Draft_slots_must_reference_existing_exercises_of_that_part()
    {
        var opinion = await CreateAsync(SpeakingSamples.Exercise(5, "SP5-001"));

        var error = await Assert.ThrowsAsync<ValidationException>(() => new CreateSpeakingTestCommandHandler(_store, _clock)
            .Handle(new CreateSpeakingTestCommand(Admin, new SpeakingTestInput("SPK-03", "Bad", null,
                [new(opinion.Id, 1, 1), new(Guid.NewGuid(), 2, 1)])), CancellationToken.None));

        Assert.Equal(2, error.Errors.Count(failure => failure.PropertyName == "Exercises"));
    }

    [Fact]
    public async Task Direct_draft_keeps_its_content_private_and_publishes_only_when_every_part_is_complete()
    {
        var image = await new UploadSpeakingImageCommandHandler(_store, _store, _clock).Handle(
            new UploadSpeakingImageCommand(Admin, SpeakingSamples.Png, "image/png"), CancellationToken.None);
        var questions = new List<SpeakingTestQuestionInput>();
        foreach (var part in Enumerable.Range(1, 5))
            for (var sequence = 1; sequence <= SpeakingTiming.SlotsPerPart(part); sequence++)
                questions.Add(DirectQuestion(part, sequence, image.Id));

        var draft = await new CreateSpeakingTestCommandHandler(_store, _clock).Handle(
            new CreateSpeakingTestCommand(Admin, new SpeakingTestInput("SP-DIRECT-01", "Direct draft", "",
                [], "DIRECT", questions)), CancellationToken.None);

        Assert.Equal("DIRECT", draft.ContentMode);
        Assert.Empty(draft.Exercises);
        Assert.Equal(7, draft.Questions.Count);
        Assert.Empty(_store.Exercises);
        Assert.All(_store.Slots, slot =>
        {
            Assert.Null(slot.ExerciseId);
            Assert.NotNull(slot.ContentJson);
        });

        var published = await Publish(draft.Id, draft.Version);

        Assert.Equal("PUBLISHED", published.Status);
        Assert.Equal("DIRECT", published.ContentMode);
        Assert.Equal(11, published.Questions.Sum(question => SpeakingTiming.QuestionsPerExercise(question.Part)));

        var attempt = await new StartSpeakingAttemptCommandHandler(_store, _clock).Handle(
            new StartSpeakingAttemptCommand(SpeakingAttemptOwner.ForUser(Guid.NewGuid()),
                new StartSpeakingAttemptInput(draft.Id, "FULL")), CancellationToken.None);
        Assert.Equal(11, attempt.Questions.Count);
        Assert.All(_store.Attempts.Single().Questions, question => Assert.Null(question.ExerciseId));
    }

    [Fact]
    public async Task Direct_draft_persists_an_incomplete_question_but_cannot_publish_it()
    {
        var draft = await new CreateSpeakingTestCommandHandler(_store, _clock).Handle(
            new CreateSpeakingTestCommand(Admin, new SpeakingTestInput("SP-DIRECT-PARTIAL", "Direct draft", "",
                [], "DIRECT", [new SpeakingTestQuestionInput(1, 1,
                    new SpeakingTestQuestionContentInput(null, null, Passage: "Draft passage"))])),
            CancellationToken.None);

        var question = Assert.Single(draft.Questions);
        Assert.Equal((1, 1, "Draft passage", ""),
            (question.Part, question.SequenceNumber, question.Content.Passage, question.Content.ModelAnswer));
        Assert.Empty(_store.Exercises);

        var error = await Assert.ThrowsAsync<SpeakingPublishRequirementsException>(() =>
            Publish(draft.Id, draft.Version));

        Assert.Contains("Part1", error.Errors.Keys);
        Assert.Equal(SpeakingTestStatus.Draft, _store.Tests.Single().Status);
    }

    [Fact]
    public async Task Summary_counts_exercises_and_tests_by_status()
    {
        await FullTestAsync();
        var archived = await CreateAsync(SpeakingSamples.Exercise(5, "SP5-ARCH"));
        await new ArchiveSpeakingExerciseCommandHandler(_store, _clock).Handle(
            new ArchiveSpeakingExerciseCommand(Admin, archived.Id, archived.Version), CancellationToken.None);

        var summary = await new GetSpeakingAdminSummaryQueryHandler(_store).Handle(new GetSpeakingAdminSummaryQuery(),
            CancellationToken.None);

        Assert.Equal(new SpeakingExerciseCountsDto(8, 7, 0, 1), summary.Exercises);
        Assert.Equal(new SpeakingTestCountsDto(1, 0, 1), summary.Tests);
    }

    [Fact]
    public async Task Deleting_a_draft_removes_the_test_and_its_slots_but_keeps_the_exercises()
    {
        var draft = await FullTestAsync();
        var exerciseCount = _store.Exercises.Count;

        await DeleteDraft(draft.Id, draft.Version);

        Assert.Empty(_store.Tests);
        Assert.Empty(_store.Slots);
        Assert.Equal(exerciseCount, _store.Exercises.Count);
    }

    [Fact]
    public async Task Deleting_a_test_is_refused_when_missing_stale_or_published()
    {
        var draft = await FullTestAsync();

        await Assert.ThrowsAsync<SpeakingNotFoundException>(() => DeleteDraft(Guid.NewGuid(), 1));
        await Assert.ThrowsAsync<SpeakingConflictException>(() => DeleteDraft(draft.Id, draft.Version + 1));
        Assert.Single(_store.Tests);

        var published = await Publish(draft.Id, draft.Version);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => DeleteDraft(published.Id, published.Version));
        Assert.Single(_store.Tests);
        Assert.NotEmpty(_store.Slots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deleting_a_test_requires_a_positive_version(int version)
    {
        var result = new DeleteSpeakingTestDraftCommandValidator().Validate(
            new DeleteSpeakingTestDraftCommand(Admin, Guid.NewGuid(), version));

        Assert.Contains(result.Errors, failure => failure.PropertyName == "ExpectedVersion");
    }

    internal static async Task<SpeakingTestDetailDto> CreatePublishedTestAsync(InMemorySpeaking store,
        TimeProvider clock)
    {
        var tests = new SpeakingAdministrationTests(store, clock);
        var test = await tests.FullTestAsync();
        return await tests.Publish(test.Id, test.Version);
    }

    private SpeakingAdministrationTests(InMemorySpeaking store, TimeProvider clock)
    {
        _store = store;
        _clock = clock as MutableTimeProvider ?? new MutableTimeProvider(clock.GetUtcNow());
    }

    public SpeakingAdministrationTests()
    {
    }

    private Task<SpeakingExerciseDto> CreateAsync(SpeakingExerciseInput input) =>
        new CreateSpeakingExerciseCommandHandler(_store, _clock).Handle(new CreateSpeakingExerciseCommand(Admin, input),
            CancellationToken.None);

    private Task<SpeakingTestDetailDto> Publish(Guid testId, int version) =>
        new PublishSpeakingTestCommandHandler(_store, _clock).Handle(new PublishSpeakingTestCommand(Admin, testId,
            version), CancellationToken.None);

    private Task DeleteDraft(Guid testId, int version) =>
        new DeleteSpeakingTestDraftCommandHandler(_store).Handle(new DeleteSpeakingTestDraftCommand(Admin, testId,
            version), CancellationToken.None);

    private Task<SpeakingImageDto> UploadImageAsync(byte[] png) =>
        new UploadSpeakingImageCommandHandler(_store, _store, _clock).Handle(
            new UploadSpeakingImageCommand(Admin, png, "image/png"), CancellationToken.None);

    private async Task<SpeakingTestDetailDto> FullTestAsync()
    {
        var slots = new List<SpeakingTestSlotInput>();
        foreach (var part in Enumerable.Range(1, 5))
            for (var sequence = 1; sequence <= SpeakingTiming.SlotsPerPart(part); sequence++)
            {
                Guid? imageId = part == 2 ? (await UploadImageAsync(SpeakingSamples.PngVariant((byte)sequence))).Id : null;
                var exercise = await CreateAsync(SpeakingSamples.Exercise(part, $"SP{part}-00{sequence}", imageId));
                slots.Add(new SpeakingTestSlotInput(exercise.Id, part, sequence));
            }
        return await new CreateSpeakingTestCommandHandler(_store, _clock).Handle(new CreateSpeakingTestCommand(Admin,
            new SpeakingTestInput("SPK-01", "TOEIC Speaking Practice Test 01", "Đề đủ 11 câu.", slots)),
            CancellationToken.None);
    }

    private static SpeakingTestQuestionInput DirectQuestion(int part, int sequence, Guid imageAssetId)
    {
        var sample = SpeakingSamples.Exercise(part, $"DIRECT-{part}-{sequence}", imageAssetId);
        return new SpeakingTestQuestionInput(part, sequence, new SpeakingTestQuestionContentInput(sample.RubricNotes,
            sample.ModelAnswer, sample.Passage, sample.ImageAssetId, sample.ImageAlt, sample.Context,
            sample.Questions, sample.DocumentTitle, sample.DocumentRows, sample.CallerIntro, sample.Prompt));
    }
}
