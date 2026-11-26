using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class ExamRevisionPersistenceTests
{
    [Fact]
    public void Forked_revision_graph_is_inserted_when_added_to_a_tracked_exam()
    {
        using var context = new ToeicDbContext(new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test").Options);
        var exam = Exam.Create("FORK-TEST", "Fork test", Guid.NewGuid(), DateTimeOffset.UtcNow);
        context.Exams.Attach(exam);
        var source = Assert.Single(exam.Revisions);
        source.Publish();

        var fork = exam.CreateDraftRevision(source.Id, DateTimeOffset.UtcNow);
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, context.Entry(fork).State);
        Assert.All(fork.Blocks, block =>
        {
            Assert.Equal(EntityState.Added, context.Entry(block).State);
            Assert.All(block.Questions, question => Assert.Equal(EntityState.Added, context.Entry(question).State));
            Assert.All(block.ContextBlocks, item => Assert.Equal(EntityState.Added, context.Entry(item).State));
        });
    }
}
