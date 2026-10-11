namespace TOEICMaster.Domain.Entities;

public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
    }

    private ExternalIdentity(Guid id, Guid userId, string provider, string subject, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        Subject = subject;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ExternalIdentity Create(Guid userId, string provider, string subject,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return new ExternalIdentity(Guid.NewGuid(), userId, provider, subject, createdAtUtc);
    }
}
