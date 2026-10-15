using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Infrastructure.Security;

public sealed class PasswordResetTokenService(IOptions<PasswordResetOptions> options) : IPasswordResetTokenService
{
    public TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(options.Value.TokenLifetimeMinutes);

    public IssuedPasswordResetToken Issue()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new IssuedPasswordResetToken(token, Hash(token));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
