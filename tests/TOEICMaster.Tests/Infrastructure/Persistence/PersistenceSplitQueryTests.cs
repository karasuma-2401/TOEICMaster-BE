using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.ExamAttempts;
using TOEICMaster.Infrastructure.Persistence.Exams;
using TOEICMaster.Infrastructure.Persistence.Seeders;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class PersistenceSplitQueryTests
{
    [Fact]
    public async Task Multi_include_reads_use_split_queries_and_do_not_track_entities()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES")
            ?? throw new InvalidOperationException("TOEICMASTER_TEST_POSTGRES is required for persistence tests.");
        var connection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = $"toeicmaster_split_{Guid.NewGuid():N}",
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(connection.ConnectionString, provider => provider.EnableRetryOnFailure(3)).Options;
        await using var db = new ToeicDbContext(options);

        try
        {
            await db.Database.MigrateAsync();
            await ExamDataSeeder.SeedSampleExamAsync(db);

            var attemptRepo = new EfExamAttemptRepository(db);
            var examRepo = new EfExamRepository(db);
            var exam = await db.Exams.FirstAsync();
            var published = exam.Revisions.First();

            var readExam = await examRepo.FindExamAsync(exam.Id, CancellationToken.None);
            Assert.NotNull(readExam);
            Assert.NotEmpty(readExam.Revisions);
            Assert.NotEmpty(readExam.Revisions.First().Blocks);
            Assert.NotEmpty(readExam.Revisions.First().Blocks.First().Questions);

            db.ChangeTracker.Clear();

            var publishedRevision = await attemptRepo.FindPublishedRevisionForExamAsync(exam.Id, CancellationToken.None);
            Assert.NotNull(publishedRevision);
            Assert.NotEmpty(publishedRevision.Blocks);
            Assert.Empty(db.ChangeTracker.Entries());

            var revisionById = await attemptRepo.FindRevisionAsync(published.Id, CancellationToken.None);
            Assert.NotNull(revisionById);
            Assert.NotEmpty(revisionById.Blocks);
            Assert.Empty(db.ChangeTracker.Entries());

            var scoreProfile = await attemptRepo.FindScoreProfileAsync(published.ScoreProfileId, CancellationToken.None);
            Assert.NotNull(scoreProfile);
            Assert.Equal(202, scoreProfile.Conversions.Count);
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
