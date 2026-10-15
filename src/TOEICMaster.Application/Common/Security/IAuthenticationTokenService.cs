using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Security;

public interface IAuthenticationTokenService
{
    IssuedAuthenticationTokens IssueForLogin(User user, DateTimeOffset issuedAtUtc, bool rememberMe);
    IssuedAuthenticationTokens Rotate(User user, DateTimeOffset issuedAtUtc, DateTimeOffset sessionExpiresAtUtc);
    string HashRefreshToken(string refreshToken);
}

public sealed record IssuedAuthenticationTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    string RefreshTokenHash,
    DateTimeOffset RefreshTokenExpiresAtUtc);
