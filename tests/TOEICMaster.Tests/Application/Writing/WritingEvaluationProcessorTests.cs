using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Writing;

public sealed class WritingEvaluationProcessorTests
{
    [Theory]
    [InlineData(WritingAttemptMode.Part1, false)]
    [InlineData(WritingAttemptMode.Part2, false)]
    [InlineData(WritingAttemptMode.Part3, false)]
    [InlineData(WritingAttemptMode.Full, true)]
    public async Task Partial_and_practice_results_never_store_a_full_test_estimate(WritingAttemptMode mode, bool practice)
    {
        var repository = new Repository(mode, practice);
        await new WritingEvaluationProcessor(repository, new Evaluator(), TimeProvider.System).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(WritingEvaluationStatus.Completed, repository.Evaluation.Status);
        Assert.Null(repository.Evaluation.EstimatedScore);
        Assert.Equal(8, repository.Evaluation.Feedback.Count);
    }

    [Fact]
    public async Task Full_test_retains_the_documented_internal_conversion()
    {
        var repository = new Repository(WritingAttemptMode.Full, false);
        await new WritingEvaluationProcessor(repository, new Evaluator(), TimeProvider.System).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(200, repository.Evaluation.EstimatedScore);
        Assert.Equal(28, repository.Evaluation.Feedback.Sum(item => item.CriterionScore));
    }

    [Fact]
    public async Task Long_text_is_bounded_without_retrying_or_splitting_a_surrogate_pair()
    {
        var repository = new Repository(WritingAttemptMode.Full, false);
        var evaluator = new Evaluator { FeedbackText = new string('a', 1498) + "😀" + new string('b', 100) };
        await new WritingEvaluationProcessor(repository, evaluator, TimeProvider.System).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(WritingEvaluationStatus.Completed, repository.Evaluation.Status);
        Assert.Equal(0, repository.Evaluation.RetryCount);
        Assert.All(repository.Evaluation.Feedback, item =>
        {
            Assert.InRange(item.Feedback.Length, 1, 1500);
            Assert.False(char.IsHighSurrogate(item.Feedback[^2]));
        });
    }

    [Theory]
    [InlineData(1498)]
    [InlineData(1499)]
    public async Task Sentence_boundary_leaves_room_for_the_ellipsis(int sentenceLength)
    {
        var repository = new Repository(WritingAttemptMode.Full, false);
        var evaluator = new Evaluator { FeedbackText = new string('a', sentenceLength) + "." + new string('b', 100) };
        await new WritingEvaluationProcessor(repository, evaluator, TimeProvider.System).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(WritingEvaluationStatus.Completed, repository.Evaluation.Status);
        Assert.All(repository.Evaluation.Feedback, item => Assert.InRange(item.Feedback.Length, 1, 1500));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invalid_scores_and_missing_questions_are_retried_instead_of_fabricated(bool invalidScore)
    {
        var repository = new Repository(WritingAttemptMode.Full, false);
        var evaluator = new Evaluator { InvalidScore = invalidScore, MissingQuestion = !invalidScore };
        await new WritingEvaluationProcessor(repository, evaluator, TimeProvider.System).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(WritingEvaluationStatus.Retrying, repository.Evaluation.Status);
        Assert.Equal("INVALID_RESPONSE", repository.Evaluation.FailureCode);
        Assert.Null(repository.Evaluation.EstimatedScore);
        Assert.Empty(repository.Evaluation.Feedback);
    }

    private sealed class Evaluator : IWritingEvaluator
    {
        public string FeedbackText { get; init; } = "Specific feedback.";
        public bool InvalidScore { get; init; }
        public bool MissingQuestion { get; init; }
        public Task<WritingEvaluatorResult> EvaluateAsync(WritingEvaluationWork work, CancellationToken cancellationToken)
        {
            var feedback = work.Answers.Select(answer => new WritingEvaluationFeedbackResult(answer.Position,
                InvalidScore ? 5 : answer.Part == 1 ? 3 : answer.Part == 2 ? 4 : 5,
                FeedbackText, "Suggested revision.")).ToArray();
            return Task.FromResult(new WritingEvaluatorResult("Summary.", [], [], MissingQuestion ? feedback[..^1] : feedback));
        }
    }

    private sealed class Repository(WritingAttemptMode mode, bool practice) : IWritingEvaluationRepository
    {
        public WritingEvaluation Evaluation { get; } = WritingEvaluation.Create(Guid.NewGuid(), "gemini", "test", "v2", DateTimeOffset.UtcNow);
        public Task<WritingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc, CancellationToken cancellationToken)
        {
            Evaluation.GrantConsentAndQueue(now, "gemini", "test", "v2");
            Evaluation.BeginProcessing(now, leaseExpiresAtUtc);
            var answers = Enumerable.Range(1, 8).Select(position => new WritingEvaluationAnswer(position,
                position <= 5 ? 1 : position <= 7 ? 2 : 3, "{}", "{}", "Answer")).ToArray();
            return Task.FromResult<WritingEvaluationWork?>(new(Evaluation.Id, "gemini", "test", "v2", answers, mode, practice));
        }
        public Task<WritingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken) => Task.FromResult<WritingEvaluation?>(Evaluation);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void AddFeedback(IReadOnlyCollection<WritingEvaluationFeedback> feedback) { }
        public Task<WritingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) => Task.FromResult<WritingEvaluation?>(Evaluation);
        public Task<WritingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) => Task.FromResult<WritingEvaluation?>(Evaluation);
        public Task<IReadOnlyDictionary<Guid, WritingEvaluation>> GetByAttemptIdsAsync(IReadOnlyCollection<Guid> attemptIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<Guid, WritingEvaluation>>(new Dictionary<Guid, WritingEvaluation>());
        public Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId, DateTimeOffset afterUtc, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(WritingEvaluation evaluation, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
