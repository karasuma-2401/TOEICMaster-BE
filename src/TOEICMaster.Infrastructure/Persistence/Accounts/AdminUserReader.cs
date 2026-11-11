using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Accounts;

public sealed class AdminUserReader(ToeicDbContext context) : IAdminUserReader
{
    public async Task<AdminUserPageDto> GetPageAsync(GetAdminUsersQuery query, CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();
        var stats = await ReadStatsAsync(users, cancellationToken);
        var filtered = ApplyFilters(users, query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)(((long)totalItems + query.PageSize - 1) / query.PageSize);
        var offset = (query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new AdminUserPageDto([], query.Page, query.PageSize, totalItems, totalPages, stats);

        var rows = await BuildPageQuery(filtered, context.UserSessions.AsNoTracking(), query.Sort, offset, query.PageSize)
            .ToListAsync(cancellationToken);
        return new AdminUserPageDto(rows.Select(ToDto).ToArray(), query.Page, query.PageSize, totalItems, totalPages,
            stats);
    }

    private static async Task<AdminUserStatsDto> ReadStatsAsync(IQueryable<User> users,
        CancellationToken cancellationToken)
    {
        var groups = await users
            .GroupBy(user => new { user.IsActive, Verified = user.EmailVerifiedAtUtc != null })
            .Select(group => new { group.Key.IsActive, group.Key.Verified, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return new AdminUserStatsDto(
            groups.Sum(group => group.Count),
            groups.Where(group => group.IsActive).Sum(group => group.Count),
            groups.Where(group => !group.IsActive && !group.Verified).Sum(group => group.Count),
            groups.Where(group => !group.IsActive && group.Verified).Sum(group => group.Count));
    }

    internal static IQueryable<User> ApplyFilters(IQueryable<User> users, GetAdminUsersQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{EscapeLikePattern(query.Q.Trim())}%";
            users = users.Where(user => EF.Functions.ILike(user.DisplayName, pattern, "\\") ||
                                        EF.Functions.ILike(user.Email, pattern, "\\"));
        }

        users = query.Role switch
        {
            AdminUserValues.RoleUser => users.Where(user => user.Role == UserRole.User),
            AdminUserValues.RoleAdmin => users.Where(user => user.Role == UserRole.Admin),
            _ => users
        };
        return query.Status switch
        {
            AdminUserValues.StatusActive => users.Where(user => user.IsActive),
            AdminUserValues.StatusPending => users.Where(user => !user.IsActive && user.EmailVerifiedAtUtc == null),
            AdminUserValues.StatusLocked => users.Where(user => !user.IsActive && user.EmailVerifiedAtUtc != null),
            _ => users
        };
    }

    internal static IQueryable<AdminUserRow> BuildPageQuery(IQueryable<User> users, IQueryable<UserSession> sessions,
        string sort, int offset, int pageSize)
    {
        var rows = users.Select(user => new AdminUserRow
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            EmailVerifiedAtUtc = user.EmailVerifiedAtUtc,
            HasPassword = user.PasswordHash != null,
            CreatedAtUtc = user.CreatedAtUtc,
            // A refresh creates a replacement session, so only sessions that replaced nothing are real logins.
            LastLoginAtUtc = sessions
                .Where(session => session.UserId == user.Id &&
                                  !sessions.Any(previous => previous.ReplacedBySessionId == session.Id))
                .Max(session => (DateTimeOffset?)session.CreatedAtUtc)
        });

        var ordered = sort switch
        {
            "fullName:asc" => rows.OrderBy(row => row.DisplayName),
            "fullName:desc" => rows.OrderByDescending(row => row.DisplayName),
            "createdAt:asc" => rows.OrderBy(row => row.CreatedAtUtc),
            // Users who never signed in stay at the end in both directions.
            "lastLoginAt:asc" => rows.OrderBy(row => row.LastLoginAtUtc == null).ThenBy(row => row.LastLoginAtUtc),
            "lastLoginAt:desc" => rows.OrderBy(row => row.LastLoginAtUtc == null)
                .ThenByDescending(row => row.LastLoginAtUtc),
            _ => rows.OrderByDescending(row => row.CreatedAtUtc)
        };
        return ordered.ThenBy(row => row.Id).Skip(offset).Take(pageSize);
    }

    internal static AdminUserDto ToDto(AdminUserRow row) => new(row.Id, row.DisplayName, row.Email,
        // The avatar endpoint only serves the signed-in user, so the list falls back to initials.
        AvatarUrl: null,
        row.Role == UserRole.Admin ? AdminUserValues.RoleAdmin : AdminUserValues.RoleUser,
        ToStatus(row.IsActive, row.EmailVerifiedAtUtc),
        row.EmailVerifiedAtUtc is not null,
        row.HasPassword ? AdminUserValues.ProviderPassword : AdminUserValues.ProviderGoogle,
        row.CreatedAtUtc, row.LastLoginAtUtc);

    private static string ToStatus(bool isActive, DateTimeOffset? emailVerifiedAtUtc)
    {
        if (isActive)
            return AdminUserValues.StatusActive;
        return emailVerifiedAtUtc is null ? AdminUserValues.StatusPending : AdminUserValues.StatusLocked;
    }

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}

internal sealed class AdminUserRow
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; init; }
    public bool HasPassword { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginAtUtc { get; init; }
}
