using TOEICMaster.Application.Authentication;

namespace TOEICMaster.Api.Contracts;

public sealed record AuthenticationResponse(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AuthenticatedUserResponse User)
{
    public static AuthenticationResponse From(AuthenticationResult result) =>
        new(result.TokenType, result.AccessToken, result.AccessTokenExpiresAtUtc, result.RefreshToken,
            result.RefreshTokenExpiresAtUtc,
            new AuthenticatedUserResponse(result.User.Id, result.User.DisplayName, result.User.Email));
}

public sealed record AuthenticatedUserResponse(Guid Id, string DisplayName, string Email);
