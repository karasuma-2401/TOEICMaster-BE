using BCrypt.Net;
using TOEICMaster.Application.Common.Security;
using Bcrypt = BCrypt.Net.BCrypt;

namespace TOEICMaster.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        Bcrypt.EnhancedHashPassword(password, WorkFactor, HashType.SHA384);

    public bool Verify(string password, string passwordHash) =>
        Bcrypt.EnhancedVerify(password, passwordHash, HashType.SHA384);
}
