using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Security;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Accounts;

public sealed class AccountsBearerApiTests
{
    [Fact]
    public async Task Ordinary_dev_login_token_does_not_grant_access_to_other_accounts()
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IAccountReader, SampleAccountReader>()));
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var token = factory.Services.GetRequiredService<IAuthenticationTokenService>()
            .IssueForLogin(user, DateTimeOffset.UtcNow, false).AccessToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/v1/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    [InlineData("Operator", HttpStatusCode.Forbidden)]
    [InlineData("User,Admin", HttpStatusCode.OK)]
    public async Task Trusted_role_claims_use_the_existing_dev_bearer_configuration(string roles, HttpStatusCode expected)
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IAccountReader, SampleAccountReader>()));
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTime.UtcNow;
        // Test-only privileged tokens: the dev login flow currently issues no role claims.
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(roles.Split(',').Select(role => new Claim(ClaimTypes.Role, role))),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(jwt.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/v1/accounts");
        Assert.Equal(expected, response.StatusCode);
    }
}
