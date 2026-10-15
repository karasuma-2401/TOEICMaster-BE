using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Interfaces;

public interface IUserRegistrationRepository
{
    Task<RegistrationAccount?> FindByNormalizedEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken);

    Task<bool> TryAddAsync(User user, EmailVerificationChallenge challenge,
        CancellationToken cancellationToken);

    Task<bool> TryReplaceVerificationAsync(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, TimeSpan cooldown, CancellationToken cancellationToken);

    Task ReleaseVerificationCooldownAsync(Guid userId, DateTimeOffset sentAtUtc, TimeSpan cooldown,
        CancellationToken cancellationToken);

    Task<EmailVerificationAttempt> VerifyEmailAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, CancellationToken cancellationToken);
}

public sealed record RegistrationAccount(Guid UserId, string Email, bool IsActive, bool IsEmailVerified);

public enum EmailVerificationAttempt
{
    Invalid,
    Verified
}
