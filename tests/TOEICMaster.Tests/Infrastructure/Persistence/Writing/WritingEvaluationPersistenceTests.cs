using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Writing;

namespace TOEICMaster.Tests.Infrastructure.Persistence.Writing;

public sealed class WritingEvaluationPersistenceTests
{
    [Fact]
    public void New_feedback_for_an_existing_evaluation_is_inserted()
    {
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new ToeicDbContext(options);
        var repository = new EfWritingEvaluationRepository(context);
        var now = DateTimeOffset.UtcNow;
        var evaluation = WritingEvaluation.Create(Guid.NewGuid(), "gemini", "test-model", "v1", now);
        evaluation.GrantConsentAndQueue(now, "gemini", "test-model", "v1");
        evaluation.BeginProcessing(now, now.AddMinutes(2));
        context.Attach(evaluation);

        var feedback = WritingEvaluationFeedback.Create(evaluation.Id, 1, 2, "Feedback", "Suggestion");
        evaluation.Complete(130, "Summary", [], [], [feedback], now);
        repository.AddFeedback([feedback]);
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, context.Entry(feedback).State);
        Assert.Equal(EntityState.Modified, context.Entry(evaluation).State);
    }
}
