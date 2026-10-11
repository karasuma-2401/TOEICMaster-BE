using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests;

public sealed class FakeUserRegistrationRepository : IUserRegistrationRepository
{
    public bool ExistingEmail { get; set; }
    public bool AddSucceeds { get; set; } = true;
    public User? AddedUser { get; private set; }

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ExistingEmail);
    }

    public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddedUser = user;
        return Task.FromResult(AddSucceeds);
    }
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string? Password { get; private set; }
    public string? VerificationPassword { get; private set; }
    public string? VerificationHash { get; private set; }

    public string Hash(string password)
    {
        Password = password;
        return "hashed-for-test";
    }

    public bool Verify(string password, string? passwordHash)
    {
        VerificationPassword = password;
        VerificationHash = passwordHash;
        return passwordHash == "hashed-for-test" && password == Password;
    }
}
