using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Infrastructure.Security;

public sealed class EmailVerificationCodeService(IOptions<EmailVerificationOptions> options)
    : IEmailVerificationCodeService
{
    private readonly byte[] _hashKey = Encoding.UTF8.GetBytes(options.Value.HashKey);

    public TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(options.Value.LifetimeMinutes);
    public TimeSpan ResendCooldown { get; } = TimeSpan.FromSeconds(options.Value.ResendCooldownSeconds);
    public int MaxFailedAttempts { get; } = options.Value.MaxFailedAttempts;

    public IssuedEmailVerificationCode Issue(string normalizedEmail)
    {
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        return new IssuedEmailVerificationCode(code, Hash(normalizedEmail, code));
    }

    public string Hash(string normalizedEmail, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{normalizedEmail}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, payload));
    }
}
