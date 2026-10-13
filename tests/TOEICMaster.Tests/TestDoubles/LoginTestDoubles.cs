using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class FakeAuthenticationRepository : IAuthenticationRepository
{
    public User? User { get; set; }
    public UserSession? AddedSession { get; private set; }
    public AuthenticationSession? ActiveSession { get; set; }
    public bool RotateSucceeds { get; set; } = true;
    public UserSession? ReplacementSession { get; private set; }
    public string? SearchedEmail { get; private set; }
    public string? RevokedTokenHash { get; private set; }

    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SearchedEmail = normalizedEmail;
        return Task.FromResult(User);
    }

    public Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddedSession = session;
        return Task.CompletedTask;
    }

    public Task<AuthenticationSession?> FindActiveSessionAsync(string refreshTokenHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ActiveSession);
    }

    public Task<bool> TryRotateSessionAsync(Guid currentSessionId, UserSession replacement,
        DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReplacementSession = replacement;
        return Task.FromResult(RotateSucceeds);
    }

    public Task RevokeSessionAsync(string refreshTokenHash, DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RevokedTokenHash = refreshTokenHash;
        return Task.CompletedTask;
    }
}

public sealed class FakeAuthenticationTokenService : IAuthenticationTokenService
{
    public bool? RememberMe { get; private set; }

    public IssuedAuthenticationTokens IssueForLogin(User user, DateTimeOffset issuedAtUtc, bool rememberMe)
    {
        RememberMe = rememberMe;
        var refreshExpiry = issuedAtUtc.AddDays(rememberMe ? 30 : 1);
        return CreateTokens(issuedAtUtc, refreshExpiry, "login-refresh-token");
    }

    public IssuedAuthenticationTokens Rotate(User user, DateTimeOffset issuedAtUtc,
        DateTimeOffset sessionExpiresAtUtc) =>
        CreateTokens(issuedAtUtc, sessionExpiresAtUtc, "rotated-refresh-token");

    public string HashRefreshToken(string refreshToken) => $"hash:{refreshToken}";

    private IssuedAuthenticationTokens CreateTokens(DateTimeOffset issuedAtUtc, DateTimeOffset refreshExpiry,
        string refreshToken) =>
        new("access-token", issuedAtUtc.AddMinutes(15), refreshToken, HashRefreshToken(refreshToken), refreshExpiry);
}

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

public sealed class FakeGoogleIdentityVerifier : IGoogleIdentityVerifier
{
    public VerifiedGoogleAccount Account { get; set; } =
        new("google-subject", "learner@gmail.com", "Google Learner", true);

    public Exception? Exception { get; set; }
    public string? Credential { get; private set; }

    public Task<VerifiedGoogleAccount> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Credential = idToken;
        return Exception is null ? Task.FromResult(Account) : Task.FromException<VerifiedGoogleAccount>(Exception);
    }
}

public sealed class FakeExternalLoginRepository : IExternalLoginRepository
{
    public ExternalLoginResolution Resolution { get; set; } = ExternalLoginResolution.LinkRequired();
    public VerifiedGoogleAccount? Account { get; private set; }

    public Task<ExternalLoginResolution> ResolveGoogleAccountAsync(VerifiedGoogleAccount account,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Account = account;
        return Task.FromResult(Resolution);
    }
}
