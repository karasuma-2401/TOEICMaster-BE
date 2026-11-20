using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        }
        finally
        {
            await firstContext.Database.EnsureDeletedAsync();
        }
    }
}
