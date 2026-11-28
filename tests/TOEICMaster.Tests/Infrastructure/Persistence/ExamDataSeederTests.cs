using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Seeders;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class ExamDataSeederTests
{
    [Fact]
    public async Task Concurrent_startups_seed_the_sample_exam_and_score_profile_once()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES")
            ?? throw new InvalidOperationException("TOEICMASTER_TEST_POSTGRES is required for persistence tests.");
        var connection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = $"toeicmaster_seed_{Guid.NewGuid():N}",
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(connection.ConnectionString, provider => provider.EnableRetryOnFailure(3)).Options;
        await using var firstContext = new ToeicDbContext(options);

        try
        {
            await firstContext.Database.MigrateAsync();
            await using var secondContext = new ToeicDbContext(options);

            await Task.WhenAll(
                ExamDataSeeder.SeedSampleExamAsync(firstContext),
                ExamDataSeeder.SeedSampleExamAsync(secondContext));

            await using var verificationContext = new ToeicDbContext(options);
            Assert.Equal(1, await verificationContext.Exams.CountAsync());
            Assert.Equal(1, await verificationContext.ScoreProfiles.CountAsync());
            Assert.Equal(202, await verificationContext.ScoreConversions.CountAsync());
            var sample = await verificationContext.Exams.Include(exam => exam.Revisions).SingleAsync();
            Assert.Equal(ExamRevisionStatus.Draft, sample.Revisions.Single().Status);

            // Simulate the previously published sample and verify startup withdraws it safely.
            var originalRevisionId = sample.Revisions.Single().Id;
            sample.PublishDraft(originalRevisionId);
            await verificationContext.SaveChangesAsync();
            await using var upgradeContext = new ToeicDbContext(options);
            await ExamDataSeeder.SeedSampleExamAsync(upgradeContext);
            await using var upgradedContext = new ToeicDbContext(options);
            var revisions = await upgradedContext.ExamRevisions.OrderBy(revision => revision.VersionNumber).ToListAsync();
            Assert.Equal(2, revisions.Count);
            Assert.Equal(originalRevisionId, revisions[0].Id);
            Assert.Equal(ExamRevisionStatus.Superseded, revisions[0].Status);
            Assert.Equal(ExamRevisionStatus.Draft, revisions[1].Status);
        }
        finally
        {
            await firstContext.Database.EnsureDeletedAsync();
        }
    }
}
