using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.ExerciseBank;

public sealed class LrPracticeApplicationTests
{
    [Fact]
    public async Task Submit_grades_case_insensitive_keys()
    {
        var item = CreateItem();
        var attempts = new RecordingAttemptRepository();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            attempts);

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
            attempts);

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
    [InlineData("E")]
    [InlineData("ABCDEFGH1")]
    public async Task Submit_rejects_invalid_selected_keys(string selectedKey)
    {
        var item = CreateItem();
        var handler = new SubmitLrPracticeCommandHandler(
            new StubExerciseBankRepository(item),
            new RecordingAttemptRepository());

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
            new RecordingAttemptRepository());

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
            new RecordingAttemptRepository());

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

    [Fact]
    public async Task Statistics_query_rejects_invalid_part()
    {
        var handler = new GetLrPracticeStatisticsQueryHandler(new RecordingAttemptRepository());

        await Assert.ThrowsAsync<InvalidLrPracticeRequestException>(() => handler.Handle(
            new GetLrPracticeStatisticsQuery(Guid.NewGuid(), 8),
            CancellationToken.None));
    }

    private static LrExerciseBankItem CreateItem() => LrExerciseBankItem.Create(
        LrExercisePart.Part1,
        "P1-001",
        "Office",
        LrExerciseDifficulty.Easy,
        LrExerciseStatus.Published,
        null,
        null,
        null,
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
        public Task<LrExerciseItemPageDto> GetPageAsync(LrExerciseItemFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new LrExerciseItemPageDto([], filter.PageNumber, filter.PageSize, 0));

        public Task<(IReadOnlyList<LrExerciseBankItem> Items, int TotalCount)> GetPublishedPageAsync(
            int part,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<LrExerciseBankItem>)[], 0));

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
