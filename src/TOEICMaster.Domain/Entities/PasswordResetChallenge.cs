namespace TOEICMaster.Domain.Entities;

public sealed class PasswordResetChallenge
{
    private PasswordResetChallenge()
    {
    }

    private PasswordResetChallenge(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        UserId = userId;
        CodeHash = codeHash;
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }
    public string? ResetTokenHash { get; private set; }
    public DateTimeOffset? ResetTokenExpiresAtUtc { get; private set; }

    public static PasswordResetChallenge Create(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc) => new(userId, codeHash, sentAtUtc, expiresAtUtc);

    public bool CanVerify(DateTimeOffset nowUtc, int maxFailedAttempts) =>
        ResetTokenHash is null && nowUtc < ExpiresAtUtc && FailedAttempts < maxFailedAttempts;
}
