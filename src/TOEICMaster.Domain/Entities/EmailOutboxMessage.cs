namespace TOEICMaster.Domain.Entities;

public enum EmailOutboxStatus : short
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage()
    {
    }

    private EmailOutboxMessage(Guid id, string to, string subject, string textBody, DateTimeOffset createdAtUtc)
    {
        Id = id;
        To = to;
        Subject = subject;
        TextBody = textBody;
        Status = EmailOutboxStatus.Pending;
        AttemptCount = 0;
        NextAttemptAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string To { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string TextBody { get; private set; } = string.Empty;
    public EmailOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static EmailOutboxMessage Create(string to, string subject, string textBody, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return new EmailOutboxMessage(Guid.NewGuid(), to.Trim(), subject.Trim(), textBody, createdAtUtc);
    }

    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        Status = EmailOutboxStatus.Sent;
        SentAtUtc = sentAtUtc;
        LastError = null;
    }

    public void RecordFailure(string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts, bool isTransient)
    {
        AttemptCount++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        if (!isTransient || AttemptCount >= maxAttempts)
        {
            Status = EmailOutboxStatus.Failed;
            return;
        }

        Status = EmailOutboxStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
