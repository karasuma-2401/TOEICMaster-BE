namespace TOEICMaster.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, string displayName, string email, string normalizedEmail, string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User CreateLocal(string displayName, string email, string normalizedEmail, string passwordHash,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), displayName, email, normalizedEmail, passwordHash, createdAtUtc);
}
