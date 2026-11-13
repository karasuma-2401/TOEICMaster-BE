using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.ExamAttempts;

public sealed class ExamAttemptApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly InMemoryExamAttempts _repository = new();
    private readonly MutableTimeProvider _clock = new(Now);

    private Task<ExamAttemptSheetDto> StartAsync() =>
        new StartExamAttemptHandler(_repository, _clock)
            .Handle(new StartExamAttemptCommand(UserId, _repository.Exam.Id), CancellationToken.None);

    private Task SaveAsync(Guid attemptId, params SaveAttemptAnswerInput[] answers) =>
        new SaveExamAttemptAnswersHandler(_repository, _clock)
            .Handle(new SaveExamAttemptAnswersCommand(UserId, attemptId, answers), CancellationToken.None);

    private Task<ExamAttemptResultDto> SubmitAsync(Guid attemptId) =>
        new SubmitExamAttemptHandler(_repository, _clock)
            .Handle(new SubmitExamAttemptCommand(UserId, attemptId), CancellationToken.None);

    private Guid Q(int number) => _repository.QuestionId(number);

    [Fact]
    public async Task Starting_uses_the_server_time_limit_and_resuming_returns_the_same_attempt()
    {
        var first = await StartAsync();
        _clock.Advance(60);
        var second = await StartAsync();

        Assert.Equal(first.AttemptId, second.AttemptId);
        Assert.Equal(7200, first.TimeLimitSeconds);
        Assert.Equal(ExamAttemptPhases.Listening, first.Phase);
        Assert.Equal(200, first.TotalQuestions);
        Assert.Equal("LR-TEST-001", first.ExamCode);
        Assert.Equal(7140, second.RemainingSeconds);
        Assert.Single(_repository.Attempts);
    }

    [Fact]
    public async Task Overview_reports_exam_facts_and_the_attempt_to_resume()
    {
        var empty = await new GetLrExamOverviewHandler(_repository)
            .Handle(new GetLrExamOverviewQuery(UserId, _repository.Exam.Id), CancellationToken.None);
        var started = await StartAsync();
        var resumed = await new GetLrExamOverviewHandler(_repository)
            .Handle(new GetLrExamOverviewQuery(UserId, _repository.Exam.Id), CancellationToken.None);

        Assert.Equal(200, empty.TotalQuestions);
        Assert.Equal(100, empty.ListeningQuestions);
        Assert.Equal(100, empty.ReadingQuestions);
        Assert.Equal(120, empty.DurationMinutes);
        Assert.Equal(990, empty.MaxScaledScore);
        Assert.Null(empty.ActiveAttemptId);
        Assert.Equal(started.AttemptId, resumed.ActiveAttemptId);
    }

    [Fact]
    public async Task Saved_answers_and_flags_come_back_when_the_attempt_is_resumed()
    {
        var sheet = await StartAsync();
        await SaveAsync(
            sheet.AttemptId,
            new SaveAttemptAnswerInput(Q(1), " a ", true),
            new SaveAttemptAnswerInput(Q(101), "C"));

        var resumed = await new GetExamAttemptProgressHandler(_repository, _clock)
            .Handle(new GetExamAttemptProgressQuery(UserId, sheet.AttemptId), CancellationToken.None);

        var questions = resumed.Blocks.SelectMany(block => block.Questions).ToDictionary(question => question.Number);
        Assert.Equal("A", questions[1].UserSelectedKey);
        Assert.True(questions[1].IsFlagged);
        Assert.Equal("C", questions[101].UserSelectedKey);
        Assert.Null(questions[7].UserSelectedKey);
    }

    [Fact]
    public async Task Saving_rejects_an_option_that_does_not_exist_for_the_question()
    {
        var sheet = await StartAsync();

        await Assert.ThrowsAsync<ExamAttemptValidationException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(InMemoryExamAttempts.PartTwoQuestion), "D")));
        await Assert.ThrowsAsync<ExamAttemptValidationException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "E")));
    }

    [Fact]
    public async Task Saving_rejects_a_question_that_is_not_part_of_the_attempt()
    {
        var sheet = await StartAsync();

        await Assert.ThrowsAsync<ExamAttemptValidationException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Guid.NewGuid(), "A")));
    }

    [Fact]
    public async Task Another_user_cannot_touch_the_attempt()
    {
        var sheet = await StartAsync();

        await Assert.ThrowsAsync<ExamAttemptNotFoundException>(() =>
            new SaveExamAttemptAnswersHandler(_repository, _clock).Handle(
                new SaveExamAttemptAnswersCommand(Guid.NewGuid(), sheet.AttemptId, []),
                CancellationToken.None));
    }

    [Fact]
    public async Task Starting_reading_locks_listening_answers_and_flags_on_the_server()
    {
        var sheet = await StartAsync();
        await SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "A"));

        var reading = await new StartReadingHandler(_repository, _clock)
            .Handle(new StartReadingCommand(UserId, sheet.AttemptId), CancellationToken.None);

        Assert.Equal(ExamAttemptPhases.Reading, reading.Phase);
        Assert.NotNull(reading.ReadingStartedAtUtc);
        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "B")));
        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            new ToggleQuestionFlagHandler(_repository, _clock).Handle(
                new ToggleQuestionFlagCommand(UserId, sheet.AttemptId, Q(1), true),
                CancellationToken.None));

        await SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(InMemoryExamAttempts.PartFiveQuestion), "C"));
    }

    [Fact]
    public async Task Submitting_grades_the_attempt_and_counts_unanswered_questions_as_skipped()
    {
        var sheet = await StartAsync();
        await SaveAsync(
            sheet.AttemptId,
            new SaveAttemptAnswerInput(Q(1), "A"),
            new SaveAttemptAnswerInput(Q(InMemoryExamAttempts.PartFiveQuestion), "A"));
        _clock.Advance(1800);

        var result = await SubmitAsync(sheet.AttemptId);

        Assert.Equal(1, result.TotalCorrect);
        Assert.Equal(1, result.TotalIncorrect);
        Assert.Equal(198, result.TotalSkipped);
        Assert.Equal(1, result.ListeningRawScore);
        Assert.Equal(0, result.ReadingRawScore);
        Assert.Equal(10, result.ListeningScaledScore);
        Assert.Equal(5, result.ReadingScaledScore);
        Assert.Equal(15, result.TotalScaledScore);
        Assert.Equal(990, result.MaxScaledScore);
        Assert.Equal(1800, result.DurationSeconds);
        Assert.False(result.IsTimeExpired);
        Assert.Equal(new[] { 6, 25, 39, 30, 30, 16, 54 }, result.Parts.Select(part => part.TotalQuestions));

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() => SubmitAsync(sheet.AttemptId));
        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "B")));
    }

    [Fact]
    public async Task Result_can_be_reloaded_after_submission_but_not_before()
    {
        var sheet = await StartAsync();
        var handler = new GetExamAttemptResultHandler(_repository);

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            handler.Handle(new GetExamAttemptResultQuery(UserId, sheet.AttemptId), CancellationToken.None));

        var submitted = await SubmitAsync(sheet.AttemptId);
        var reloaded = await handler.Handle(new GetExamAttemptResultQuery(UserId, sheet.AttemptId), CancellationToken.None);

        Assert.Equal(submitted.TotalScaledScore, reloaded.TotalScaledScore);
        Assert.Equal("Test exam", reloaded.ExamTitle);
    }

    [Fact]
    public async Task Review_reports_status_per_question_and_filters_blocks()
    {
        var sheet = await StartAsync();
        await SaveAsync(
            sheet.AttemptId,
            new SaveAttemptAnswerInput(Q(1), "A", true),
            new SaveAttemptAnswerInput(Q(InMemoryExamAttempts.PartTwoQuestion), "A"));
        await SubmitAsync(sheet.AttemptId);
        var handler = new GetExamAttemptReviewHandler(_repository);

        var all = await handler.Handle(
            new GetExamAttemptReviewQuery(UserId, sheet.AttemptId), CancellationToken.None);
        var incorrect = await handler.Handle(
            new GetExamAttemptReviewQuery(UserId, sheet.AttemptId, ExamReviewFilter.Incorrect), CancellationToken.None);
        var flagged = await handler.Handle(
            new GetExamAttemptReviewQuery(UserId, sheet.AttemptId, ExamReviewFilter.Flagged), CancellationToken.None);

        var allQuestions = all.Blocks.SelectMany(block => block.Questions).ToDictionary(question => question.Number);
        Assert.Equal(200, allQuestions.Count);
        Assert.Equal(ExamReviewStatuses.Correct, allQuestions[1].Status);
        Assert.Equal(ExamReviewStatuses.Wrong, allQuestions[7].Status);
        Assert.Equal("B", allQuestions[7].CorrectKey);
        Assert.Equal(ExamReviewStatuses.Skipped, allQuestions[101].Status);
        Assert.Equal("Explanation", allQuestions[1].Explanation);
        Assert.Equal(1, all.Counts.Correct);
        Assert.Equal(1, all.Counts.Wrong);
        Assert.Equal(198, all.Counts.Skipped);
        Assert.Equal(1, all.Counts.Flagged);

        Assert.DoesNotContain(incorrect.Blocks.SelectMany(block => block.Questions), question => question.Number == 1);
        Assert.Equal(199, incorrect.Blocks.Sum(block => block.Questions.Count));
        Assert.Equal(1, flagged.Blocks.Sum(block => block.Questions.Count));
    }

    [Fact]
    public async Task Review_is_not_available_before_submission()
    {
        var sheet = await StartAsync();

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            new GetExamAttemptReviewHandler(_repository)
                .Handle(new GetExamAttemptReviewQuery(UserId, sheet.AttemptId), CancellationToken.None));
    }

    [Fact]
    public async Task Resuming_after_the_deadline_auto_submits_at_the_deadline_and_reports_a_conflict()
    {
        var sheet = await StartAsync();
        await SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "A"));
        _clock.Advance(7200 + 600);

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            new GetExamAttemptProgressHandler(_repository, _clock)
                .Handle(new GetExamAttemptProgressQuery(UserId, sheet.AttemptId), CancellationToken.None));

        var attempt = Assert.Single(_repository.Attempts);
        Assert.Equal(ExamAttemptStatus.Completed, attempt.Status);
        Assert.True(attempt.IsTimeExpired);
        Assert.Equal(Now.AddSeconds(7200), attempt.SubmittedAtUtc);
        Assert.Equal(1, attempt.ListeningRawScore);
    }

    [Fact]
    public async Task Saving_is_rejected_once_the_deadline_and_grace_period_have_passed()
    {
        var sheet = await StartAsync();
        _clock.Advance(7200 + 6);

        await Assert.ThrowsAsync<ExamAttemptConflictException>(() =>
            SaveAsync(sheet.AttemptId, new SaveAttemptAnswerInput(Q(1), "A")));
    }

    [Fact]
    public async Task Starting_again_after_an_abandoned_deadline_closes_the_old_attempt_and_opens_a_new_one()
    {
        var first = await StartAsync();
        _clock.Advance(7200 + 600);

        var second = await StartAsync();

        Assert.NotEqual(first.AttemptId, second.AttemptId);
        Assert.Equal(2, _repository.Attempts.Count);
        Assert.Equal(ExamAttemptStatus.Completed, _repository.Attempts[0].Status);
        Assert.True(_repository.Attempts[0].IsTimeExpired);
        Assert.Equal(7200, second.RemainingSeconds);
    }
}
