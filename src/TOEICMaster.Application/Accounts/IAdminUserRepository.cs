using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Accounts;

public interface IAdminUserRepository
{
    Task<AdminUserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> TryCreateAsync(User user, EmailVerificationChallenge? challenge, CancellationToken cancellationToken);

    Task<AdminUserDto> UpdateRoleAsync(Guid actorUserId, Guid targetUserId, UserRole role, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<string?> DeleteAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task LockAsync(Guid actorUserId, Guid targetUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task UnlockAsync(Guid targetUserId, CancellationToken cancellationToken);

    Task<AdminPasswordResetTarget?> GetPasswordResetTargetAsync(Guid targetUserId,
        CancellationToken cancellationToken);
}

public sealed record AdminPasswordResetTarget(
    Guid UserId,
    string Email,
    string NormalizedEmail,
    bool IsActive,
    bool EmailVerified,
    bool HasPassword);
