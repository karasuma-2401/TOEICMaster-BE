using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class ExamRevisionTrackingTests
{
    [Fact]
    public void Forked_graph_is_tracked_as_new_rows()
    {
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var db = new ToeicDbContext(options);
        var exam = Exam.Create("TRACKER", "Tracker", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var published = exam.Revisions.Single();
        published.ReplaceContextBlocks("P3-G32", [new ExamContextContent(1, ExamContextType.Text, "Passage", null, null)]);
        exam.PublishDraft(published.Id);
        db.Exams.Attach(exam);

        var clone = exam.CreateDraftRevision(published.Id, DateTimeOffset.UtcNow);
        db.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, db.Entry(clone).State);
        Assert.All(clone.Blocks, block => Assert.Equal(EntityState.Added, db.Entry(block).State));
        Assert.All(clone.Blocks.SelectMany(block => block.Questions), question =>
            Assert.Equal(EntityState.Added, db.Entry(question).State));
        Assert.All(clone.Blocks.SelectMany(block => block.ContextBlocks), context =>
            Assert.Equal(EntityState.Added, db.Entry(context).State));
    }
}
