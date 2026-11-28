using TOEICMaster.Application.Exams;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.ExerciseBank;

public sealed class LrPracticeApplicationTests
{
    [Fact]
    public async Task Listing_resolves_stored_media_keys_for_public_items()
    {
        var item = CreateItem();
        item.UpdateMediaAndStatus("lr-exercise/audio/example.mp3", "lr-exercise/image/example.png",
            LrExerciseStatus.Published, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var handler = new GetLrPracticeItemsQueryHandler(new StubExerciseBankRepository(item),
            new PresigningMediaStore());

        var page = await handler.Handle(new GetLrPracticeItemsQuery(1, 1, 20), CancellationToken.None);

        var listed = Assert.Single(page.Items);
        Assert.Equal("https://media.test/lr-exercise/audio/example.mp3", listed.AudioUrl);
        Assert.Equal("https://media.test/lr-exercise/image/example.png", listed.ImageUrl);
    }

    [Fact]
    public async Task Submit_grades_case_insensitive_keys()
    {
        var item = CreateItem();
        var attempts = new RecordingAttemptRepository();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            attempts,
            TimeProvider.System);

        var result = await handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [
                    new LrPracticeAnswerInput(item.Questions.Single().Id, " a ")
                ])),
            CancellationToken.None);

        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(0, result.IncorrectCount);
        var savedAttempt = Assert.Single(attempts.AddedAttempts);
        Assert.Equal("A", Assert.Single(savedAttempt.Answers).SelectedKey);
    }

    [Fact]
    public async Task Submit_persists_unanswered_questions_as_skipped()
    {
        var item = CreateItem();
        var attempts = new RecordingAttemptRepository();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            attempts,
            TimeProvider.System);

        var result = await handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest([])),
            CancellationToken.None);

        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(1, result.IncorrectCount);
        Assert.Null(attempts.AddedAttempts[0].Answers.Single().SelectedKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_treats_null_or_whitespace_selected_keys_as_skipped(string? selectedKey)
    {
        var item = CreateItem();
        var attempts = new RecordingAttemptRepository();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            attempts,
            TimeProvider.System);

        var result = await handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [
                    new LrPracticeAnswerInput(item.Questions.Single().Id, selectedKey)
                ])),
            CancellationToken.None);

        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(1, result.IncorrectCount);
        Assert.Null(attempts.AddedAttempts[0].Answers.Single().SelectedKey);
    }

    [Fact]
    public async Task Submit_rejects_null_answers_collection()
    {
        var item = CreateItem();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidLrPracticeSubmissionException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(null!)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_rejects_question_id_belonging_to_another_item()
    {
        var item = CreateItem();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidLrPracticeSubmissionException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [new LrPracticeAnswerInput(Guid.NewGuid(), "A")])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_rejects_option_d_for_part_2_question()
    {
        var item = LrExerciseBankItem.Create(
            LrExercisePart.Part2,
            "P2-001",
            "Question-Response",
            LrExerciseDifficulty.Easy,
            LrExerciseStatus.Published,
            null,
            "https://example.test/audio.mp3",
            null,
            [new LrExerciseBankQuestionInput(
                "P2-001-Q1",
                "Where is the meeting?",
                "In room 201",
                "At 3 PM",
                "Yes, I will",
                null,
                "A",
                "Translation",
                "Explanation")],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidLrPracticeSubmissionException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [new LrPracticeAnswerInput(item.Questions.Single().Id, "D")])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_grades_grouped_item_with_correct_wrong_and_skipped_answers()
    {
        var item = LrExerciseBankItem.Create(
            LrExercisePart.Part3,
            "P3-001",
            "Conversation",
            LrExerciseDifficulty.Medium,
            LrExerciseStatus.Published,
            "M: Where is the report? W: On your desk.",
            "https://example.test/audio.mp3",
            null,
            [
                new LrExerciseBankQuestionInput("P3-001-Q1", "Q1", "A1", "B1", "C1", "D1", "A", "T1", "E1"),
                new LrExerciseBankQuestionInput("P3-001-Q2", "Q2", "A2", "B2", "C2", "D2", "B", "T2", "E2"),
                new LrExerciseBankQuestionInput("P3-001-Q3", "Q3", "A3", "B3", "C3", "D3", "C", "T3", "E3")
            ],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var questions = item.Questions.OrderBy(x => x.Ordinal).ToArray();
        var attempts = new RecordingAttemptRepository();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            attempts,
            TimeProvider.System);

        var result = await handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [
                    new LrPracticeAnswerInput(questions[0].Id, "a"),
                    new LrPracticeAnswerInput(questions[1].Id, "D")
                ])),
            CancellationToken.None);

        Assert.Equal(item.Id, result.ItemId);
        Assert.Equal(3, result.TotalQuestions);
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(2, result.IncorrectCount);
        Assert.True(result.Questions[0].IsCorrect);
        Assert.Equal("A", result.Questions[0].SelectedKey);
        Assert.False(result.Questions[1].IsCorrect);
        Assert.Equal("D", result.Questions[1].SelectedKey);
        Assert.False(result.Questions[2].IsCorrect);
        Assert.Null(result.Questions[2].SelectedKey);
        Assert.Equal("E3", result.Questions[2].Explanation);
    }

    [Theory]
    [InlineData("E")]
    [InlineData("ABCDEFGH1")]
    public async Task Submit_rejects_invalid_selected_keys(string selectedKey)
    {
        var item = CreateItem();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidLrPracticeSubmissionException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [new LrPracticeAnswerInput(item.Questions.Single().Id, selectedKey)])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_rejects_duplicate_question_ids()
    {
        var item = CreateItem();
        var questionId = item.Questions.Single().Id;
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidLrPracticeSubmissionException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                item.Id,
                new LrPracticeSubmitRequest(
                [
                    new LrPracticeAnswerInput(questionId, "A"),
                    new LrPracticeAnswerInput(questionId, "B")
                ])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_maps_missing_item_to_typed_not_found_exception()
    {
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(null),
            new RecordingAttemptRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<LrPracticeItemNotFoundException>(() => handler.Handle(
            new SubmitLrPracticeCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new LrPracticeSubmitRequest([])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Statistics_query_delegates_to_database_aggregate_repository()
    {
        var expected = new[]
        {
            new LrPracticeItemStatisticsDto(Guid.NewGuid(), 2, 3, 4)
        };
        var repository = new RecordingAttemptRepository { Statistics = expected };
        var handler = new GetLrPracticeStatisticsQueryHandler(repository);

        var actual = await handler.Handle(
            new GetLrPracticeStatisticsQuery(Guid.NewGuid(), 3),
            CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.Equal(3, repository.StatisticsPart);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Statistics_query_rejects_invalid_part(int part)
    {
        var handler = new GetLrPracticeStatisticsQueryHandler(new RecordingAttemptRepository());

        await Assert.ThrowsAsync<InvalidLrPracticeRequestException>(() => handler.Handle(
            new GetLrPracticeStatisticsQuery(Guid.NewGuid(), part),
            CancellationToken.None));
    }

    [Fact]
    public async Task Statistics_query_rejects_empty_user_id()
    {
        var handler = new GetLrPracticeStatisticsQueryHandler(new RecordingAttemptRepository());

        await Assert.ThrowsAsync<InvalidLrPracticeRequestException>(() => handler.Handle(
            new GetLrPracticeStatisticsQuery(Guid.Empty, 1),
            CancellationToken.None));
    }

    private static LrExerciseBankItem CreateItem() => LrExerciseBankItem.Create(
        LrExercisePart.Part1,
        "P1-001",
        "Office",
        LrExerciseDifficulty.Easy,
        LrExerciseStatus.Published,
        null,
        "https://example.test/audio.mp3",
        "https://example.test/image.png",
        [new LrExerciseBankQuestionInput(
            "P1-001-Q1",
            "What is shown?",
            "A report",
            "A flight",
            "A meeting",
            "A lunch",
            "A",
            "Translation",
            "Explanation")],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private sealed class StubExerciseBankRepository(LrExerciseBankItem? item) : ILrExerciseBankRepository
    {
        public Task<LrExerciseBankItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
            Task.FromResult(itemId == item?.Id ? item : null);
        public Task<LrExerciseItemPageDto> GetPageAsync(LrExerciseItemFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new LrExerciseItemPageDto([], filter.PageNumber, filter.PageSize, 0));

        public Task<(IReadOnlyList<LrExerciseBankItem> Items, int TotalCount)> GetPublishedPageAsync(
            int part,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<LrExerciseBankItem>)(item is null ? [] : [item]), item is null ? 0 : 1));

        public Task<LrExerciseBankItem?> GetPublishedByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
            Task.FromResult(item);

        public Task<LrExerciseBankSummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LrExerciseBankSummaryDto(0, null, []));

        public Task<bool> ItemCodeExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddItemsAsync(IReadOnlyCollection<LrExerciseBankItem> items, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PresigningMediaStore : IExamMediaStore
    {
        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan lifetime,
            CancellationToken cancellationToken) => Task.FromResult($"https://media.test/{objectKey}");
    }

    private sealed class RecordingAttemptRepository : ILrPracticeAttemptRepository
    {
        public List<LrPracticeAttempt> AddedAttempts { get; } = [];
        public IReadOnlyList<LrPracticeItemStatisticsDto> Statistics { get; init; } = [];
        public int StatisticsPart { get; private set; }

        public Task AddAsync(LrPracticeAttempt attempt, CancellationToken cancellationToken)
        {
            AddedAttempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LrPracticeAttempt>> GetByUserAndItemAsync(
            Guid userId,
            Guid itemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LrPracticeAttempt>>([]);

        public Task<IReadOnlyList<LrPracticeItemStatisticsDto>> GetStatisticsByUserAndPartAsync(
            Guid userId,
            int part,
            CancellationToken cancellationToken)
        {
            StatisticsPart = part;
            return Task.FromResult(Statistics);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
