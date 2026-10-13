using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class FakeUserRegistrationRepository : IUserRegistrationRepository
{
    public bool ExistingEmail { get; set; }
    public bool ExistingPendingEmail { get; set; }
    public bool AddSucceeds { get; set; } = true;
    public bool ReplaceSucceeds { get; set; } = true;
    public EmailVerificationAttempt VerificationResult { get; set; } = EmailVerificationAttempt.Verified;
    public User? AddedUser { get; private set; }
    public EmailVerificationChallenge? AddedChallenge { get; private set; }
    public string? ReplacementCodeHash { get; private set; }
    public string? VerifiedCodeHash { get; private set; }
    public string? VerifiedEmail { get; private set; }
    public bool CooldownReleased { get; private set; }

    public Task<RegistrationAccount?> FindByNormalizedEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RegistrationAccount? account = ExistingEmail
            ? new RegistrationAccount(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "learner@example.com", true, true)
            : ExistingPendingEmail
                ? new RegistrationAccount(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    "learner@example.com", false, false)
                : null;
        return Task.FromResult(account);
    }

    public Task<bool> TryAddAsync(User user, EmailVerificationChallenge challenge,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddedUser = user;
        AddedChallenge = challenge;
        return Task.FromResult(AddSucceeds);
    }

    public Task<bool> TryReplaceVerificationAsync(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, TimeSpan cooldown, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReplacementCodeHash = codeHash;
        return Task.FromResult(ReplaceSucceeds);
    }

    public Task ReleaseVerificationCooldownAsync(Guid userId, DateTimeOffset sentAtUtc, TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CooldownReleased = true;
        return Task.CompletedTask;
    }

    public Task<EmailVerificationAttempt> VerifyEmailAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifiedEmail = normalizedEmail;
        VerifiedCodeHash = codeHash;
        return Task.FromResult(VerificationResult);
    }
}

public sealed class FakeEmailVerificationCodeService : IEmailVerificationCodeService
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxFailedAttempts { get; set; } = 5;

    public IssuedEmailVerificationCode Issue(string normalizedEmail) =>
        new("123456", Hash(normalizedEmail, "123456"));

    public string Hash(string normalizedEmail, string code) => $"hash:{normalizedEmail}:{code}";
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string? Password { get; private set; }
    public string? VerificationPassword { get; private set; }
    public string? VerificationHash { get; private set; }

    public string Hash(string password)
    {
        Password = password;
        return "hashed-for-test";
    }

    public bool Verify(string password, string? passwordHash)
    {
        VerificationPassword = password;
        VerificationHash = passwordHash;
        return passwordHash == "hashed-for-test" && password == Password;
    }
}
