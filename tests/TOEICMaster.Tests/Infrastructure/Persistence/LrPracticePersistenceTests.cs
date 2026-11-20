using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
