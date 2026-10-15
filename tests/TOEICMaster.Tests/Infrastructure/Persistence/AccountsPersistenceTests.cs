using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Accounts;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class AccountsPersistenceTests
{
    private static ToeicDbContext CreateContext() => new(new DbContextOptionsBuilder<ToeicDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test").Options);

    [Fact]
    public void Dev_migrations_match_the_current_model_without_a_second_account_schema()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains("20261011163038_InitialUsers", context.Database.GetMigrations());
        Assert.Contains("20261013150528_AddEmailVerification", context.Database.GetMigrations());
        Assert.DoesNotContain(context.Database.GetMigrations(), migration => migration.Contains("InitialAuth"));
        Assert.DoesNotContain(context.Database.GetMigrations(), migration => migration.Contains("CreateAccounts"));
    }

    [Fact]
    public void Account_page_sql_paginates_dev_users_without_private_authentication_fields()
    {
        using var context = CreateContext();
        var sql = AccountReader.BuildPageQuery(context.Users.AsNoTracking(), 10, 10).ToQueryString();
        Assert.Contains("users", sql);
        Assert.Contains("DisplayName", sql);
        Assert.Contains("CreatedAtUtc", sql);
        Assert.DoesNotContain("user_roles", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("PasswordHash", sql);
        Assert.DoesNotContain("NormalizedEmail", sql);
    }

    [Fact]
    public void Existing_dev_schema_is_used_without_adding_role_or_account_tables()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Matches("CREATE TABLE \"?users\"?\\s", script);
        Assert.DoesNotMatch("CREATE TABLE \"?roles\"?\\s", script);
        Assert.DoesNotMatch("CREATE TABLE \"?user_roles\"?\\s", script);
        Assert.DoesNotContain("CREATE TABLE \"Accounts\"", script);
    }
}
