using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.ExerciseBank;
using TOEICMaster.Infrastructure.Persistence.Migrations;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class LrPracticePersistenceTests
{
    [Fact]
    public void Current_model_has_no_pending_changes()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Practice_migration_creates_tables_indexes_and_foreign_keys()
    {
        var operations = new AddLrPracticeAttemptsProbe().GetUpOperations();
        var attempts = Assert.Single(operations.OfType<CreateTableOperation>(),
            operation => operation.Name == "lr_practice_attempts");
        var answers = Assert.Single(operations.OfType<CreateTableOperation>(),
            operation => operation.Name == "lr_practice_attempt_answers");

        Assert.Contains(attempts.ForeignKeys,
            foreignKey => foreignKey.PrincipalTable == "users" &&
                foreignKey.OnDelete == ReferentialAction.Cascade);
        Assert.Contains(attempts.ForeignKeys,
            foreignKey => foreignKey.PrincipalTable == "lr_exercise_bank_items" &&
                foreignKey.OnDelete == ReferentialAction.Restrict);
        Assert.Contains(operations.OfType<CreateIndexOperation>(),
            index => index.Table == "lr_practice_attempt_answers" &&
                index.IsUnique &&
                index.Columns.SequenceEqual(["AttemptId", "QuestionId"]));
        Assert.Contains(answers.ForeignKeys,
            foreignKey => foreignKey.PrincipalTable == "lr_practice_attempts" &&
                foreignKey.OnDelete == ReferentialAction.Cascade);
    }

    [Fact]
    public void Practice_migration_generates_postgresql_ddl()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(
            "20261120021557_AddKnowledgeBase",
            "20261120133659_AddLrPracticeAttempts");

        Assert.Contains("CREATE TABLE lr_practice_attempts", script);
        Assert.Contains("CREATE TABLE lr_practice_attempt_answers", script);
        Assert.Contains("IX_lr_practice_attempt_answers_AttemptId_QuestionId", script);
    }

    [Fact]
    public void Statistics_query_aggregates_attempts_in_postgresql()
    {
        using var context = CreateContext();
        var repository = new EfLrPracticeAttemptRepository(context);
        var sql = repository.BuildStatisticsQuery(Guid.NewGuid(), 3).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("count(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UserId", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Repositories_persist_attempts_and_aggregate_statistics_in_postgresql()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES")
            ?? throw new InvalidOperationException("TOEICMASTER_TEST_POSTGRES is required for persistence tests.");
        var connection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = $"toeicmaster_lr_practice_{Guid.NewGuid():N}",
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(connection.ConnectionString, provider => provider.EnableRetryOnFailure(3)).Options;

        await using var db = new ToeicDbContext(options);
        try
        {
            await db.Database.MigrateAsync();

            var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
            var userA = User.CreateExternal("User A", "lr-a@example.com", "LR-A@EXAMPLE.COM", now);
            var userB = User.CreateExternal("User B", "lr-b@example.com", "LR-B@EXAMPLE.COM", now);

            var publishedPart3 = LrExerciseBankItem.Create(
                LrExercisePart.Part3,
                "P3-PG-001",
                "Office",
                LrExerciseDifficulty.Medium,
                LrExerciseStatus.Published,
                "M: Ready? W: Yes.",
                "https://example.test/audio.mp3",
                null,
                [
                    new LrExerciseBankQuestionInput("P3-PG-001-Q1", "Q1", "A1", "B1", "C1", "D1", "A", "T1", "E1"),
                    new LrExerciseBankQuestionInput("P3-PG-001-Q2", "Q2", "A2", "B2", "C2", "D2", "B", "T2", "E2"),
                    new LrExerciseBankQuestionInput("P3-PG-001-Q3", "Q3", "A3", "B3", "C3", "D3", "C", "T3", "E3")
                ],
                userA.Id,
                now);
            var draftPart3 = LrExerciseBankItem.Create(
                LrExercisePart.Part3,
                "P3-PG-DRAFT",
                "Draft",
                LrExerciseDifficulty.Easy,
                LrExerciseStatus.Draft,
                "Draft context",
                null,
                null,
                [
                    new LrExerciseBankQuestionInput("P3-PG-D-Q1", "Q1", "A1", "B1", "C1", "D1", "A", null, null),
                    new LrExerciseBankQuestionInput("P3-PG-D-Q2", "Q2", "A2", "B2", "C2", "D2", "B", null, null),
                    new LrExerciseBankQuestionInput("P3-PG-D-Q3", "Q3", "A3", "B3", "C3", "D3", "C", null, null)
                ],
                userA.Id,
                now);
            var publishedPart5 = LrExerciseBankItem.Create(
                LrExercisePart.Part5,
                "P5-PG-001",
                "Grammar",
                LrExerciseDifficulty.Easy,
                LrExerciseStatus.Published,
                null,
                null,
                null,
                [
                    new LrExerciseBankQuestionInput("P5-PG-001-Q1", "Q1", "A1", "B1", "C1", "D1", "D", "T5", "E5")
                ],
                userA.Id,
                now);

            db.Users.AddRange(userA, userB);
            db.LrExerciseBankItems.AddRange(publishedPart3, draftPart3, publishedPart5);
            await db.SaveChangesAsync();

            var bankRepo = new EfLrExerciseBankRepository(db);
            var (pageItems, totalCount) = await bankRepo.GetPublishedPageAsync(3, 1, 20, CancellationToken.None);
            Assert.Equal(1, totalCount);
            Assert.Equal(publishedPart3.Id, Assert.Single(pageItems).Id);
            Assert.Null(await bankRepo.GetPublishedByIdAsync(draftPart3.Id, CancellationToken.None));

            var p3Questions = publishedPart3.Questions.OrderBy(q => q.Ordinal).ToArray();
            var p5Question = publishedPart5.Questions.Single();

            var attemptRepo = new EfLrPracticeAttemptRepository(db);
            await attemptRepo.AddAsync(LrPracticeAttempt.Create(
                userA.Id,
                publishedPart3.Id,
                [
                    new LrPracticeAnswerSnapshot(p3Questions[0].Id, 1, "A", "A", true, "T1", "E1"),
                    new LrPracticeAnswerSnapshot(p3Questions[1].Id, 2, "D", "B", false, "T2", "E2"),
                    new LrPracticeAnswerSnapshot(p3Questions[2].Id, 3, null, "C", false, "T3", "E3")
                ],
                now.AddMinutes(1)), CancellationToken.None);
            await attemptRepo.AddAsync(LrPracticeAttempt.Create(
                userA.Id,
                publishedPart3.Id,
                [
                    new LrPracticeAnswerSnapshot(p3Questions[0].Id, 1, "A", "A", true, "T1", "E1"),
                    new LrPracticeAnswerSnapshot(p3Questions[1].Id, 2, "B", "B", true, "T2", "E2"),
                    new LrPracticeAnswerSnapshot(p3Questions[2].Id, 3, "C", "C", true, "T3", "E3")
                ],
                now.AddMinutes(2)), CancellationToken.None);
            await attemptRepo.AddAsync(LrPracticeAttempt.Create(
                userB.Id,
                publishedPart3.Id,
                [
                    new LrPracticeAnswerSnapshot(p3Questions[0].Id, 1, "A", "A", true, "T1", "E1"),
                    new LrPracticeAnswerSnapshot(p3Questions[1].Id, 2, "B", "B", true, "T2", "E2"),
                    new LrPracticeAnswerSnapshot(p3Questions[2].Id, 3, "D", "C", false, "T3", "E3")
                ],
                now.AddMinutes(3)), CancellationToken.None);
            await attemptRepo.AddAsync(LrPracticeAttempt.Create(
                userA.Id,
                publishedPart5.Id,
                [
                    new LrPracticeAnswerSnapshot(p5Question.Id, 1, "D", "D", true, "T5", "E5")
                ],
                now.AddMinutes(4)), CancellationToken.None);
            await attemptRepo.SaveChangesAsync(CancellationToken.None);

            await using var verifyDb = new ToeicDbContext(options);
            var verifyRepo = new EfLrPracticeAttemptRepository(verifyDb);

            Assert.Equal(4, await verifyDb.LrPracticeAttempts.CountAsync());
            Assert.Equal(10, await verifyDb.LrPracticeAttemptAnswers.CountAsync());

            var userAStats = await verifyRepo.GetStatisticsByUserAndPartAsync(userA.Id, 3, CancellationToken.None);
            var userAStat = Assert.Single(userAStats);
            Assert.Equal(publishedPart3.Id, userAStat.ItemId);
            Assert.Equal(2, userAStat.AttemptCount);
            Assert.Equal(3, userAStat.BestScore);
            Assert.Equal(3, userAStat.TotalQuestions);

            var userBStats = await verifyRepo.GetStatisticsByUserAndPartAsync(userB.Id, 3, CancellationToken.None);
            var userBStat = Assert.Single(userBStats);
            Assert.Equal(publishedPart3.Id, userBStat.ItemId);
            Assert.Equal(1, userBStat.AttemptCount);
            Assert.Equal(2, userBStat.BestScore);
            Assert.Equal(3, userBStat.TotalQuestions);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static ToeicDbContext CreateContext() => new(new DbContextOptionsBuilder<ToeicDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test")
        .Options);

    private sealed class AddLrPracticeAttemptsProbe : AddLrPracticeAttempts
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}
