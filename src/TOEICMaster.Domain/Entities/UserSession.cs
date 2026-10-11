namespace TOEICMaster.Domain.Entities;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(Guid id, Guid userId, string refreshTokenHash, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public static UserSession Create(Guid userId, string refreshTokenHash, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Session expiry must be after creation.");

        return new UserSession(Guid.NewGuid(), userId, refreshTokenHash, createdAtUtc, expiresAtUtc);
    }
}
