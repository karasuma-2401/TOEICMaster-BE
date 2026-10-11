using BCrypt.Net;
using TOEICMaster.Application.Common.Security;
using Bcrypt = BCrypt.Net.BCrypt;

namespace TOEICMaster.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    private const string DummyHash = "$2a$12$KIXx4EaF7VnPfDPj7uQYwO9MHPnY8i5nN6RuY1P5hNjN6w0G8jz2K";

    public string Hash(string password) =>
        Bcrypt.EnhancedHashPassword(password, WorkFactor, HashType.SHA384);

    public bool Verify(string password, string? passwordHash) =>
        Bcrypt.EnhancedVerify(password, passwordHash ?? DummyHash, HashType.SHA384);
}
