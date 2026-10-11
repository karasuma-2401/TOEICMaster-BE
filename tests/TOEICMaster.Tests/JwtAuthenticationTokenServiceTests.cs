using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests;

public sealed class JwtAuthenticationTokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-with-at-least-32-characters";

    [Fact]
    public async Task Access_token_has_a_valid_signature_and_expected_identity_claims()
    {
        var options = CreateOptions();
        var service = new JwtAuthenticationTokenService(Options.Create(options));
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateLocal("Nguyen Van A", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash", now);

        var tokens = service.IssueForLogin(user, now, false);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(1)
            });

        Assert.True(validation.IsValid, validation.Exception?.ToString());
        var token = Assert.IsType<JsonWebToken>(validation.SecurityToken);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(user.Email, token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.DisplayName, token.GetClaim(JwtRegisteredClaimNames.Name).Value);
        Assert.Equal(now.AddMinutes(15), tokens.AccessTokenExpiresAtUtc);
    }

    [Fact]
    public void Remember_me_changes_only_the_refresh_session_lifetime()
    {
        var service = new JwtAuthenticationTokenService(Options.Create(CreateOptions()));
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash", now);

        var standard = service.IssueForLogin(user, now, false);
        var remembered = service.IssueForLogin(user, now, true);

        Assert.Equal(now.AddDays(1), standard.RefreshTokenExpiresAtUtc);
        Assert.Equal(now.AddDays(30), remembered.RefreshTokenExpiresAtUtc);
        Assert.Equal(standard.AccessTokenExpiresAtUtc, remembered.AccessTokenExpiresAtUtc);
        Assert.NotEqual(standard.RefreshToken, remembered.RefreshToken);
        Assert.DoesNotContain(standard.RefreshToken, standard.RefreshTokenHash, StringComparison.Ordinal);
    }

    private static JwtOptions CreateOptions() => new()
    {
        Issuer = "TOEICMaster.Tests",
        Audience = "TOEICMaster.Tests.Client",
        SigningKey = SigningKey,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 1,
        RememberMeRefreshTokenDays = 30
    };
}
