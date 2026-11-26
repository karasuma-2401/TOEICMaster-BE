namespace TOEICMaster.Domain.Entities;

public sealed class EmailVerificationChallenge
{
    private EmailVerificationChallenge()
    {
    }

    private EmailVerificationChallenge(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, string? pendingDisplayName, string? pendingPasswordHash)
    {
        UserId = userId;
        CodeHash = codeHash;
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        PendingDisplayName = pendingDisplayName;
        PendingPasswordHash = pendingPasswordHash;
    }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string? PendingDisplayName { get; private set; }
    public string? PendingPasswordHash { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }

    public static EmailVerificationChallenge Create(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, string? pendingDisplayName = null, string? pendingPasswordHash = null) =>
        new(userId, codeHash, sentAtUtc, expiresAtUtc, pendingDisplayName, pendingPasswordHash);

    public bool CanResend(DateTimeOffset nowUtc, TimeSpan cooldown) => SentAtUtc + cooldown <= nowUtc;

    public void ReplaceCode(string codeHash, DateTimeOffset sentAtUtc, DateTimeOffset expiresAtUtc,
        string? pendingDisplayName = null, string? pendingPasswordHash = null)
    {
        CodeHash = codeHash;
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        FailedAttempts = 0;
        if (pendingDisplayName is not null)
            PendingDisplayName = pendingDisplayName;
        if (pendingPasswordHash is not null)
            PendingPasswordHash = pendingPasswordHash;
    }

    public bool CanVerify(DateTimeOffset nowUtc, int maxFailedAttempts) =>
        nowUtc < ExpiresAtUtc && FailedAttempts < maxFailedAttempts;

    public void RecordFailedAttempt() => FailedAttempts++;

    public void ReleaseCooldown(DateTimeOffset sentAtUtc, TimeSpan cooldown)
    {
        if (SentAtUtc == sentAtUtc)
            SentAtUtc = sentAtUtc - cooldown;
    }
}
