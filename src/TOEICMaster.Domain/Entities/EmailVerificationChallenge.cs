namespace TOEICMaster.Domain.Entities;

public sealed class EmailVerificationChallenge
{
    private EmailVerificationChallenge()
    {
    }

    private EmailVerificationChallenge(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
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

    public static EmailVerificationChallenge Create(Guid userId, string codeHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc) => new(userId, codeHash, sentAtUtc, expiresAtUtc);

    public bool CanResend(DateTimeOffset nowUtc, TimeSpan cooldown) => SentAtUtc + cooldown <= nowUtc;

    public void ReplaceCode(string codeHash, DateTimeOffset sentAtUtc, DateTimeOffset expiresAtUtc)
    {
        CodeHash = codeHash;
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        FailedAttempts = 0;
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
