using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests;

public sealed class BcryptPasswordHasherTests
{
    [Fact]
    public void Hash_is_salted_and_verifiable_without_storing_plaintext()
    {
        const string password = "a-long-password-that-is-not-stored";
        var hasher = new BcryptPasswordHasher();

        var firstHash = hasher.Hash(password);
        var secondHash = hasher.Hash(password);

        Assert.NotEqual(password, firstHash);
        Assert.NotEqual(firstHash, secondHash);
        Assert.True(hasher.Verify(password, firstHash));
        Assert.False(hasher.Verify("wrong-password", firstHash));
    }

    [Fact]
    public void Enhanced_hash_supports_the_frontend_password_limit()
    {
        var password = string.Concat(Enumerable.Repeat("á", 128));
        var hasher = new BcryptPasswordHasher();

        var passwordHash = hasher.Hash(password);

        Assert.True(hasher.Verify(password, passwordHash));
    }
}
