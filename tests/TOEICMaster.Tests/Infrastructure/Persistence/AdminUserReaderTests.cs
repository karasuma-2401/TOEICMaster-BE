using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Accounts;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class AdminUserReaderTests
{
    private static ToeicDbContext CreateContext() => new(new DbContextOptionsBuilder<ToeicDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test").Options);

    [Theory]
    [InlineData(true, true, "ACTIVE")]
    [InlineData(false, false, "PENDING")]
    [InlineData(false, true, "LOCKED")]
    public void Status_is_derived_from_activation_and_email_verification(bool isActive, bool verified, string expected)
    {
        var dto = AdminUserReader.ToDto(new AdminUserRow
        {
            IsActive = isActive,
            EmailVerifiedAtUtc = verified ? DateTimeOffset.UnixEpoch : null
        });
        Assert.Equal(expected, dto.Status);
        Assert.Equal(verified, dto.EmailVerified);
    }

    [Theory]
    [InlineData(true, "PASSWORD")]
    [InlineData(false, "GOOGLE")]
    public void Auth_provider_is_derived_from_the_password_hash(bool hasPassword, string expected)
        => Assert.Equal(expected, AdminUserReader.ToDto(new AdminUserRow { HasPassword = hasPassword }).AuthProvider);

    [Fact]
    public void Page_sql_reads_logins_without_selecting_private_authentication_fields()
    {
        using var context = CreateContext();
        var sql = AdminUserReader.BuildPageQuery(context.Users.AsNoTracking(), context.UserSessions.AsNoTracking(),
            "lastLoginAt:desc", 10, 10).ToQueryString();
        Assert.Contains("user_sessions", sql);
        Assert.Contains("ReplacedBySessionId", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("RefreshTokenHash", sql);
        Assert.DoesNotContain("u.\"PasswordHash\" AS", sql);
    }

    [Fact]
    public void Search_escapes_like_wildcards()
    {
        using var context = CreateContext();
        var sql = AdminUserReader.ApplyFilters(context.Users.AsNoTracking(),
            new GetAdminUsersQuery(Q: "50%_off")).ToQueryString();
        Assert.Contains("ILIKE", sql);
        Assert.Contains("%50\\%\\_off%", sql);
    }

    [Theory]
    [InlineData("USER", null, "Role")]
    [InlineData(null, "ACTIVE", "IsActive")]
    [InlineData(null, "PENDING", "EmailVerifiedAtUtc")]
    [InlineData(null, "LOCKED", "EmailVerifiedAtUtc")]
    public void Filters_are_applied_in_sql(string? role, string? status, string column)
    {
        using var context = CreateContext();
        var sql = AdminUserReader.ApplyFilters(context.Users.AsNoTracking(),
            new GetAdminUsersQuery(Role: role, Status: status)).ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains(column, sql);
    }
}
