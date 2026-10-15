using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Interfaces;

public interface IPasswordResetRepository
{
    Task<PasswordResetAccount?> FindEligibleAccountAsync(string normalizedEmail,
        CancellationToken cancellationToken);

    Task<bool> TryReplaceChallengeAsync(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, TimeSpan cooldown, CancellationToken cancellationToken);

    Task ReleaseChallengeCooldownAsync(Guid userId, DateTimeOffset sentAtUtc, TimeSpan cooldown,
        CancellationToken cancellationToken);

    Task<PasswordResetCodeAttempt> VerifyCodeAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, string resetTokenHash,
        DateTimeOffset resetTokenExpiresAtUtc, CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(string resetTokenHash, string passwordHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record PasswordResetAccount(Guid UserId, string Email);

public enum PasswordResetCodeAttempt
{
    Invalid,
    Verified
}
