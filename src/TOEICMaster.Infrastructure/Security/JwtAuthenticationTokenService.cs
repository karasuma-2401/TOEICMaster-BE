using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Security;

public sealed class JwtAuthenticationTokenService(IOptions<JwtOptions> options) : IAuthenticationTokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAuthenticationTokens IssueForLogin(User user, DateTimeOffset issuedAtUtc, bool rememberMe)
    {
        var refreshTokenDays = rememberMe
            ? _options.RememberMeRefreshTokenDays
            : _options.RefreshTokenDays;
        return Issue(user, issuedAtUtc, issuedAtUtc.AddDays(refreshTokenDays));
    }

    public IssuedAuthenticationTokens Rotate(User user, DateTimeOffset issuedAtUtc,
        DateTimeOffset sessionExpiresAtUtc) =>
        Issue(user, issuedAtUtc, sessionExpiresAtUtc);

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private IssuedAuthenticationTokens Issue(User user, DateTimeOffset issuedAtUtc,
        DateTimeOffset refreshTokenExpiresAtUtc)
    {
        if (refreshTokenExpiresAtUtc <= issuedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(refreshTokenExpiresAtUtc));

        var accessTokenExpiresAtUtc = issuedAtUtc.AddMinutes(_options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            IssuedAt = issuedAtUtc.UtcDateTime,
            NotBefore = issuedAtUtc.UtcDateTime,
            Expires = accessTokenExpiresAtUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };
        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        return new IssuedAuthenticationTokens(accessToken, accessTokenExpiresAtUtc, refreshToken,
            HashRefreshToken(refreshToken), refreshTokenExpiresAtUtc);
    }
}
