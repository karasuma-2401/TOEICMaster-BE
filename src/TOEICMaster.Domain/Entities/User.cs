namespace TOEICMaster.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, string displayName, string email, string normalizedEmail, string? passwordHash,
        UserRole role, bool isActive, DateTimeOffset? emailVerifiedAtUtc, DateTimeOffset createdAtUtc)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        EmailVerifiedAtUtc = emailVerifiedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public string? AvatarObjectKey { get; private set; }
    public UserRole Role { get; private set; } = UserRole.User;
    public string? Phone { get; private set; }
    public int? TargetScore { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User CreateLocal(string displayName, string email, string normalizedEmail, string passwordHash,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), displayName, email, normalizedEmail, passwordHash, UserRole.User, false, null, createdAtUtc);

    public static User CreateExternal(string displayName, string email, string normalizedEmail,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), displayName, email, normalizedEmail, null, UserRole.User, true, createdAtUtc, createdAtUtc);

    public void PromoteToAdmin() => Role = UserRole.Admin;

    public void VerifyEmail(DateTimeOffset verifiedAtUtc)
    {
        EmailVerifiedAtUtc ??= verifiedAtUtc;
        IsActive = true;
    }

    public void AcceptVerifiedExternalIdentity(DateTimeOffset verifiedAtUtc)
    {
        PasswordHash = null;
        VerifyEmail(verifiedAtUtc);
    }
}
