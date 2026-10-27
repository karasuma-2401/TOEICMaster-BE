using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication;

public sealed record AuthenticationResult(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AuthenticatedUser User)
{
    public static AuthenticationResult Create(User user, IssuedAuthenticationTokens tokens) =>
        new("Bearer", tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc, new AuthenticatedUser(user.Id, user.DisplayName, user.Email, user.Role));
}

public sealed record AuthenticatedUser(Guid Id, string DisplayName, string Email, UserRole Role);
