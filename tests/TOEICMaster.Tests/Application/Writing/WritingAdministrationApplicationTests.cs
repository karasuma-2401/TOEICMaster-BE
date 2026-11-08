using FluentValidation;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Writing;

public sealed class WritingAdministrationApplicationTests
{
    private static readonly Guid AdministratorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Only_a_current_draft_can_be_deleted()
    {
        var (test, exercises) = CreateDraftTest();
        var repository = new FakeWritingAdminRepository(test, exercises);
        var handler = new DeleteWritingTestDraftCommandHandler(repository);
        await Assert.ThrowsAsync<WritingConflictException>(() => handler.Handle(
            new DeleteWritingTestDraftCommand(AdministratorId, test.Id, test.Version - 1), CancellationToken.None));
        Assert.Equal(0, repository.RemoveCount);
        await handler.Handle(new DeleteWritingTestDraftCommand(AdministratorId, test.Id, test.Version), CancellationToken.None);
        Assert.Equal(1, repository.RemoveCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Published_test_is_archived_and_restored_without_changing_the_snapshot()
    {
        var (test, exercises) = CreateDraftTest();
        var repository = new FakeWritingAdminRepository(test, exercises);
        var published = await new PublishWritingTestCommandHandler(repository, TimeProvider.System).Handle(
            new PublishWritingTestCommand(AdministratorId, test.Id, test.Version), CancellationToken.None);
        var snapshot = published.Questions.First().SnapshotJson;
        var archive = new ArchiveWritingTestCommandHandler(repository, TimeProvider.System);
        await Assert.ThrowsAsync<WritingConflictException>(() => archive.Handle(
            new ArchiveWritingTestCommand(AdministratorId, test.Id, published.Version - 1), CancellationToken.None));
        await Assert.ThrowsAsync<WritingConflictException>(() => new DeleteWritingTestDraftCommandHandler(repository).Handle(
            new DeleteWritingTestDraftCommand(AdministratorId, test.Id, published.Version), CancellationToken.None));
        var archived = await archive.Handle(new ArchiveWritingTestCommand(AdministratorId, test.Id, published.Version), CancellationToken.None);
        Assert.Equal("ARCHIVED", archived.Status);
        Assert.Equal(published.PublishedAtUtc, archived.PublishedAtUtc);
        Assert.Equal(snapshot, archived.Questions.First().SnapshotJson);
        Assert.Equal(0, repository.RemoveCount);
        await Assert.ThrowsAsync<WritingConflictException>(() => archive.Handle(
            new ArchiveWritingTestCommand(AdministratorId, test.Id, archived.Version), CancellationToken.None));
        var restored = await new RestoreWritingTestCommandHandler(repository, TimeProvider.System).Handle(
            new RestoreWritingTestCommand(AdministratorId, test.Id, archived.Version), CancellationToken.None);
        Assert.Equal("PUBLISHED", restored.Status);
        Assert.Equal(snapshot, restored.Questions.First().SnapshotJson);
        Assert.Equal(published.PublishedAtUtc, restored.PublishedAtUtc);
    }

    [Fact]
    public async Task Draft_cannot_be_archived_or_restored()
    {
        var (test, exercises) = CreateDraftTest();
        var repository = new FakeWritingAdminRepository(test, exercises);
        await Assert.ThrowsAsync<WritingConflictException>(() => new ArchiveWritingTestCommandHandler(repository, TimeProvider.System).Handle(
            new ArchiveWritingTestCommand(AdministratorId, test.Id, test.Version), CancellationToken.None));
        await Assert.ThrowsAsync<WritingConflictException>(() => new RestoreWritingTestCommandHandler(repository, TimeProvider.System).Handle(
            new RestoreWritingTestCommand(AdministratorId, test.Id, test.Version), CancellationToken.None));
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Exercise_import_does_not_save_any_rows_if_one_row_is_invalid()
    {
        var valid = new WritingExerciseInput(3, "WP3-1", "Work", "EASY", "DRAFT", null, null,
            null, null, null, null, null, null, null, null, null, "Explain your opinion.", 300, null);
        var preview = new WritingExerciseImportPreviewDto(3, "part3.xlsx",
            [new WritingExerciseImportRowDto(2, "WP3-1", [], valid),
             new WritingExerciseImportRowDto(3, null, ["Thiếu mã bài."])]);
        var repository = new FakeWritingAdminRepository(null, []);
        var handler = new ImportWritingExercisesCommandHandler(new FixedImportParser(preview), repository, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidWritingImportException>(() => handler.Handle(
            new ImportWritingExercisesCommand(AdministratorId, 3, "part3.xlsx", [1]), CancellationToken.None));
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Draft_can_store_inline_question_without_a_bank_exercise()
    {
        var repository = new FakeWritingAdminRepository(null, []);
        var handler = new CreateWritingTestCommandHandler(repository, TimeProvider.System);
        var content = new WritingExerciseInput(3, "QUESTION-1", "Work", "EASY", "DRAFT", null, null,
            null, null, null, null, null, null, null, null, null, "Explain your opinion.", 300, null);
        var input = new WritingTestInput("TEST-1", "Test", null,
            [new WritingTestQuestionInput(null, 3, 1, content)]);

        Assert.True(new WritingTestInputValidator().Validate(input).IsValid);
        var result = await handler.Handle(new CreateWritingTestCommand(AdministratorId, input), CancellationToken.None);

        var question = Assert.Single(result.Questions);
        Assert.Null(question.ExerciseId);
        Assert.Contains("Explain your opinion.", question.SnapshotJson, StringComparison.Ordinal);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Publish_requires_the_complete_5_2_1_question_shape()
    {
        var (test, exercises) = CreateDraftTest(part1Count: 4);
        var repository = new FakeWritingAdminRepository(test, exercises);
        var handler = new PublishWritingTestCommandHandler(repository, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<WritingConflictException>(() => handler.Handle(
            new PublishWritingTestCommand(AdministratorId, test.Id, test.Version), CancellationToken.None));

        Assert.Contains("Part1", exception.Message, StringComparison.Ordinal);
        Assert.Equal(WritingTestStatus.Draft, test.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Publish_rejects_a_source_exercise_that_is_not_published()
    {
        var (test, exercises) = CreateDraftTest();
        exercises[0].Update(exercises[0].Code, exercises[0].Topic, exercises[0].Difficulty,
            WritingExerciseStatus.Draft, exercises[0].RubricNotes, exercises[0].ModelAnswer,
            exercises[0].ImageAssetId, exercises[0].ImageAlt, exercises[0].KeywordOne, exercises[0].KeywordTwo,
            exercises[0].Sender, exercises[0].Recipient, exercises[0].Subject, exercises[0].EmailBody,
            exercises[0].Directions, exercises[0].Prompt, exercises[0].MinimumWordCount, exercises[0].Outline,
            AdministratorId, Now);
        var handler = new PublishWritingTestCommandHandler(new FakeWritingAdminRepository(test, exercises),
            TimeProvider.System);

        await Assert.ThrowsAsync<WritingConflictException>(() => handler.Handle(
            new PublishWritingTestCommand(AdministratorId, test.Id, test.Version), CancellationToken.None));
    }

    [Fact]
    public async Task Publish_freezes_the_exercise_snapshot_before_the_bank_item_changes()
    {
        var (test, exercises) = CreateDraftTest();
        var repository = new FakeWritingAdminRepository(test, exercises);
        var handler = new PublishWritingTestCommandHandler(repository, TimeProvider.System);

        var published = await handler.Handle(new PublishWritingTestCommand(AdministratorId, test.Id, test.Version),
            CancellationToken.None);
        var snapshot = Assert.Single(published.Questions, question => question.ExerciseId == exercises[0].Id)
            .SnapshotJson;
        exercises[0].Update(exercises[0].Code, exercises[0].Topic, exercises[0].Difficulty,
            WritingExerciseStatus.Published, exercises[0].RubricNotes, "Changed after publication",
            exercises[0].ImageAssetId, exercises[0].ImageAlt, exercises[0].KeywordOne, exercises[0].KeywordTwo,
            exercises[0].Sender, exercises[0].Recipient, exercises[0].Subject, exercises[0].EmailBody,
            exercises[0].Directions, exercises[0].Prompt, exercises[0].MinimumWordCount, exercises[0].Outline,
            AdministratorId, Now.AddMinutes(1));

        Assert.NotNull(snapshot);
        Assert.Contains("Original model answer", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Changed after publication", snapshot, StringComparison.Ordinal);
        Assert.Equal("PUBLISHED", published.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public void Part_three_input_accepts_a_missing_model_answer()
    {
        var result = new WritingExerciseInputValidator().Validate(new WritingExerciseInput(
            Part: 3, Code: "WP3-001", Topic: "Work", Difficulty: "EASY", Status: "PUBLISHED",
            RubricNotes: null, ModelAnswer: null, ImageAssetId: null, ImageAlt: null,
            KeywordOne: null, KeywordTwo: null, Sender: null, Recipient: null, Subject: null, EmailBody: null,
            Directions: null, Prompt: "Explain your opinion.", MinimumWordCount: 300, Outline: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Part_one_input_requires_an_uploaded_image_and_two_keywords()
    {
        var result = new WritingExerciseInputValidator().Validate(new WritingExerciseInput(
            Part: 1, Code: "WP1-001", Topic: "Work", Difficulty: "EASY", Status: "PUBLISHED",
            RubricNotes: null, ModelAnswer: "A model answer", ImageAssetId: null, ImageAlt: null,
            KeywordOne: null, KeywordTwo: null, Sender: null, Recipient: null, Subject: null, EmailBody: null,
            Directions: null, Prompt: null, MinimumWordCount: null, Outline: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ImageAssetId");
        Assert.Contains(result.Errors, error => error.PropertyName == "KeywordOne");
        Assert.Contains(result.Errors, error => error.PropertyName == "KeywordTwo");
    }

    private static (WritingTest Test, List<WritingExercise> Exercises) CreateDraftTest(int part1Count = 5)
    {
        var exercises = new List<WritingExercise>();
        exercises.AddRange(Enumerable.Range(1, part1Count).Select(index => Exercise(WritingPart.Part1, index)));
        exercises.AddRange(Enumerable.Range(1, 2).Select(index => Exercise(WritingPart.Part2, index)));
        exercises.Add(Exercise(WritingPart.Part3, 1));
        var test = WritingTest.Create("WRT-TEST-01", "Writing test", "Description", AdministratorId, Now);
        var questions = exercises.GroupBy(exercise => exercise.Part).SelectMany(group => group.OrderBy(exercise => exercise.Code)
            .Select((exercise, index) => WritingTestQuestion.Create(test.Id, exercise.Id, exercise.Part, index + 1))).ToArray();
        test.UpdateDraft(test.Code, test.Title, test.Description, questions, AdministratorId, Now);
        return (test, exercises);
    }

    private static WritingExercise Exercise(WritingPart part, int index) => WritingExercise.Create(part,
        $"WP{(int)part}-{index:000}", "Work", WritingDifficulty.Easy, WritingExerciseStatus.Published,
        "Rubric", "Original model answer", part == WritingPart.Part1 ? Guid.NewGuid() : null,
        part == WritingPart.Part1 ? "A photo" : null, part == WritingPart.Part1 ? "walk" : null,
        part == WritingPart.Part1 ? "while" : null, part == WritingPart.Part2 ? "Sender" : null,
        part == WritingPart.Part2 ? "Recipient" : null, part == WritingPart.Part2 ? "Subject" : null,
        part == WritingPart.Part2 ? "Email body" : null, part == WritingPart.Part2 ? "Directions" : null,
        part == WritingPart.Part3 ? "Essay prompt" : null, part == WritingPart.Part3 ? 300 : null,
        part == WritingPart.Part3 ? "Outline" : null, AdministratorId, Now);

    private sealed class FakeWritingAdminRepository(WritingTest? test, IReadOnlyList<WritingExercise> exercises)
        : IWritingAdminRepository
    {
        public int SaveCount { get; private set; }
        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }

        public Task<WritingExercisePageDto> GetExercisePageAsync(WritingExerciseFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WritingExerciseDto?> GetExerciseAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(exercises.SingleOrDefault(exercise => exercise.Id == id)?.ToDto());
        public Task<WritingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(exercises.SingleOrDefault(exercise => exercise.Id == id));
        public Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(exercises.Any(exercise => exercise.Code == code && exercise.Id != excludingId));
        public Task<bool> MediaAssetExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task AddExerciseAsync(WritingExercise exercise, CancellationToken cancellationToken)
        {
            AddCount++;
            return Task.CompletedTask;
        }
        public Task<WritingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<WritingMediaAsset?>(null);
        public Task AddMediaAssetAsync(WritingMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WritingTestPageDto> GetTestPageAsync(WritingTestFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<WritingTestDto?> GetTestAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(test?.Id == id ? test.ToDto() : null);
        public Task<WritingTest?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(test?.Id == id ? test : null);
        public Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<IReadOnlyList<WritingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WritingExercise>>(exercises.Where(exercise => ids.Contains(exercise.Id)).ToArray());
        public Task AddTestAsync(WritingTest writingTest, CancellationToken cancellationToken) => Task.CompletedTask;
        public void RemoveTest(WritingTest writingTest) => RemoveCount++;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedImportParser(WritingExerciseImportPreviewDto preview) : IWritingExerciseImportParser
    {
        public WritingExerciseImportPreviewDto Preview(int part, string fileName, byte[] content) => preview;
    }
}
