using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests;

public sealed class AccountsBearerApiTests
{
    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    [InlineData("Operator", HttpStatusCode.Forbidden)]
    [InlineData("User,Admin", HttpStatusCode.OK)]
    [InlineData("Admin,User,Admin", HttpStatusCode.OK)]
    public async Task Teammate_generated_tokens_authorize_the_account_list_by_role(string role, HttpStatusCode expected)
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IAccountReader, SampleAccountReader>()));
        var tokens = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "TOEICMaster.Tests",
            Audience = "TOEICMaster.Tests.Client",
            Key = "test-only-signing-key-at-least-32-characters"
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GenerateToken(Guid.NewGuid(), "test@example.com", role.Split(',')));
        using var response = await client.GetAsync("/api/v1/accounts");
        Assert.Equal(expected, response.StatusCode);
    }
}
