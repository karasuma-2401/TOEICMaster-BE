namespace TOEICMaster.Domain.Entities;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(Guid id, Guid familyId, Guid userId, string refreshTokenHash, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        FamilyId = familyId;
        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public static UserSession Create(Guid userId, string refreshTokenHash, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc, Guid? familyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Session expiry must be after creation.");

        var id = Guid.NewGuid();
        return new UserSession(id, familyId ?? id, userId, refreshTokenHash, createdAtUtc, expiresAtUtc);
    }
}
