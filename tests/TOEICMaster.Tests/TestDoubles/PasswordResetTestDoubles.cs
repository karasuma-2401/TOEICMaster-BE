using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class FakePasswordResetRepository : IPasswordResetRepository
{
    public bool HasEligibleAccount { get; set; } = true;
    public bool ReplaceSucceeds { get; set; } = true;
    public PasswordResetCodeAttempt VerificationResult { get; set; } = PasswordResetCodeAttempt.Verified;
    public bool ResetSucceeds { get; set; } = true;
    public string? IssuedCodeHash { get; private set; }
    public string? VerifiedEmail { get; private set; }
    public string? VerifiedCodeHash { get; private set; }
    public string? SavedResetTokenHash { get; private set; }
    public DateTimeOffset? SavedResetTokenExpiresAtUtc { get; private set; }
    public string? ConsumedResetTokenHash { get; private set; }
    public string? NewPasswordHash { get; private set; }
    public DateTimeOffset? ResetAtUtc { get; private set; }
    public bool CooldownReleased { get; private set; }

    public Task<PasswordResetAccount?> FindEligibleAccountAsync(string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PasswordResetAccount? account = HasEligibleAccount
            ? new PasswordResetAccount(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                "learner@example.com")
            : null;
        return Task.FromResult(account);
    }

    public Task<bool> TryReplaceChallengeAsync(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, TimeSpan cooldown, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IssuedCodeHash = codeHash;
        return Task.FromResult(ReplaceSucceeds);
    }

    public Task ReleaseChallengeCooldownAsync(Guid userId, DateTimeOffset sentAtUtc, TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CooldownReleased = true;
        return Task.CompletedTask;
    }

    public Task<PasswordResetCodeAttempt> VerifyCodeAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, string resetTokenHash,
        DateTimeOffset resetTokenExpiresAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifiedEmail = normalizedEmail;
        VerifiedCodeHash = codeHash;
        SavedResetTokenHash = resetTokenHash;
        SavedResetTokenExpiresAtUtc = resetTokenExpiresAtUtc;
        return Task.FromResult(VerificationResult);
    }

    public Task<bool> ResetPasswordAsync(string resetTokenHash, string passwordHash,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConsumedResetTokenHash = resetTokenHash;
        NewPasswordHash = passwordHash;
        ResetAtUtc = nowUtc;
        return Task.FromResult(ResetSucceeds);
    }
}

public sealed class FakePasswordResetTokenService : IPasswordResetTokenService
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(10);

    public IssuedPasswordResetToken Issue() => new("reset-token", "hash:reset-token");

    public string Hash(string token) => $"hash:{token}";
}
