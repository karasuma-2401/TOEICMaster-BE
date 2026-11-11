using MediatR;

namespace TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

public sealed record GetAdminUsersQuery(int Page = 1, int PageSize = 10, string? Q = null, string? Role = null,
    string? Status = null, string Sort = "createdAt:desc") : IRequest<AdminUserPageDto>;

public sealed record AdminUserDto(Guid Id, string FullName, string Email, string? AvatarUrl, string Role, string Status,
    bool EmailVerified, string AuthProvider, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed record AdminUserStatsDto(int TotalUsers, int ActiveUsers, int PendingUsers, int LockedUsers);

public sealed record AdminUserPageDto(IReadOnlyList<AdminUserDto> Items, int Page, int PageSize, int TotalItems,
    int TotalPages, AdminUserStatsDto Stats);

public static class AdminUserValues
{
    public const string RoleUser = "USER";
    public const string RoleAdmin = "ADMIN";
    public const string StatusActive = "ACTIVE";
    public const string StatusPending = "PENDING";
    public const string StatusLocked = "LOCKED";
    public const string ProviderPassword = "PASSWORD";
    public const string ProviderGoogle = "GOOGLE";
}

public sealed class GetAdminUsersQueryHandler(IAdminUserReader reader)
    : IRequestHandler<GetAdminUsersQuery, AdminUserPageDto>
{
    public Task<AdminUserPageDto> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
        => reader.GetPageAsync(request, cancellationToken);
}
