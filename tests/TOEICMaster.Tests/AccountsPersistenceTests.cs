using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Accounts;

namespace TOEICMaster.Tests;

public sealed class AccountsPersistenceTests
{
    private static ToeicDbContext CreateContext() => new(new DbContextOptionsBuilder<ToeicDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test").Options);

    [Fact]
    public void Reused_user_role_migrations_match_the_current_model()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains("20261011134141_InitialAuth", context.Database.GetMigrations());
        Assert.DoesNotContain(context.Database.GetMigrations(), migration => migration.Contains("CreateAccounts"));
    }

    [Fact]
    public void Account_page_sql_paginates_users_and_projects_roles_without_password_hashes()
    {
        using var context = CreateContext();
        var sql = AccountReader.BuildPageQuery(context.Users.AsNoTracking(), 10, 10).ToQueryString();
        Assert.Contains("users", sql);
        Assert.Contains("roles", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("PasswordHash", sql);
    }

    [Fact]
    public void Existing_schema_generates_users_roles_and_user_roles_without_an_accounts_table()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Matches("CREATE TABLE \"?users\"?\\s", script);
        Assert.Matches("CREATE TABLE \"?roles\"?\\s", script);
        Assert.Matches("CREATE TABLE \"?user_roles\"?\\s", script);
        Assert.DoesNotContain("CREATE TABLE \"Accounts\"", script);
    }
}
