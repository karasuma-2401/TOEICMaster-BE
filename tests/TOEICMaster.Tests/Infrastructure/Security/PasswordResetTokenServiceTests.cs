using Microsoft.Extensions.Options;
using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests.Infrastructure.Security;

public sealed class PasswordResetTokenServiceTests
{
    [Fact]
    public void Issue_creates_random_url_safe_tokens_and_stores_only_a_sha256_hash()
    {
        var service = new PasswordResetTokenService(Options.Create(new PasswordResetOptions()));

        var first = service.Issue();
        var second = service.Issue();

        Assert.NotEqual(first.Token, second.Token);
        Assert.Matches("^[A-Za-z0-9_-]+$", first.Token);
        Assert.Equal(43, first.Token.Length);
        Assert.Equal(64, first.TokenHash.Length);
        Assert.Equal(service.Hash(first.Token), first.TokenHash);
        Assert.DoesNotContain(first.Token, first.TokenHash, StringComparison.Ordinal);
    }
}
