using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Interfaces;

public interface IAuthenticationRepository
{
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);
    Task<AuthenticationSession?> FindActiveSessionAsync(string refreshTokenHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<bool> TryRotateSessionAsync(Guid currentSessionId, UserSession replacement, DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);
    Task RevokeSessionAsync(string refreshTokenHash, DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);
}

public sealed record AuthenticationSession(UserSession Session, User User);
