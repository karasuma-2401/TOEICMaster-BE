using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Infrastructure.Security;

// Minimal JWT generator: creates a compact JWT signed with HMAC-SHA256 without external JWT packages.
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Key))
            throw new InvalidOperationException("JWT signing key is not configured.");
    }

    public string GenerateToken(Guid userId, string email, IEnumerable<string> roles)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["email"] = email,
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(_options.ExpiresMinutes).ToUnixTimeSeconds(),
            ["role"] = roles.Distinct().ToArray()
        };

        static string Base64UrlEncode(string input) => Convert.ToBase64String(Encoding.UTF8.GetBytes(input)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        static string Base64UrlEncodeBytes(byte[] input) => Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var headerJson = System.Text.Json.JsonSerializer.Serialize(header);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

        var segments = new List<string>
        {
            Base64UrlEncode(headerJson),
            Base64UrlEncode(payloadJson)
        };

        var signingInput = string.Join('.', segments);
        var signature = Sign(signingInput, _options.Key);
        segments.Add(Base64UrlEncodeBytes(signature));

        return string.Join('.', segments);
    }

    private static byte[] Sign(string input, string key)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        return hmac.ComputeHash(bytes);
    }
}
