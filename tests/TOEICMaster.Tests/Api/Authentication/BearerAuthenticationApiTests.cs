using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Authentication;

public sealed class FakeAccessTokenPrincipalValidator : IAccessTokenPrincipalValidator
{
    public bool IsValid { get; set; } = true;
    public Guid? LastUserId { get; private set; }
    public string? LastRoleClaim { get; private set; }
    public string? LastSecurityStampClaim { get; private set; }

    public Task<bool> IsValidAsync(Guid userId, string? roleClaim, string? securityStampClaim,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUserId = userId;
        LastRoleClaim = roleClaim;
        LastSecurityStampClaim = securityStampClaim;
        return Task.FromResult(IsValid);
    }
}

public sealed class BearerAuthenticationApiFactory : ApiFactory
{
    public FakeAccessTokenPrincipalValidator PrincipalValidator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccessTokenPrincipalValidator>();
            services.AddSingleton<IAccessTokenPrincipalValidator>(PrincipalValidator);
            services.AddControllers().AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
        });
    }
}

[ApiController]
[Route("tests/authenticated")]
[Authorize]
public sealed class AuthenticationProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { subject = User.FindFirst("sub")?.Value });
}

public sealed class BearerAuthenticationApiTests
{
    [Fact]
    public async Task Valid_issued_token_is_accepted_by_the_configured_bearer_handler()
    {
        using var factory = new BearerAuthenticationApiFactory();
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash",
            DateTimeOffset.UtcNow);
        var token = factory.Services.GetRequiredService<IAuthenticationTokenService>()
            .IssueForLogin(user, DateTimeOffset.UtcNow, false).AccessToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/tests/authenticated");
        var body = await response.Content.ReadFromJsonAsync<AuthenticationProbeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.Id.ToString(), body?.Subject);
        Assert.Equal(user.Id, factory.PrincipalValidator.LastUserId);
        Assert.Equal(user.Role.ToString(), factory.PrincipalValidator.LastRoleClaim);
        Assert.Equal(user.SecurityStamp.ToString(), factory.PrincipalValidator.LastSecurityStampClaim);
    }

    [Fact]
    public async Task Stale_or_revoked_principal_is_rejected_by_the_configured_bearer_handler()
    {
        using var factory = new BearerAuthenticationApiFactory();
        factory.PrincipalValidator.IsValid = false;
        var user = User.CreateLocal("Admin", "admin@example.com", "ADMIN@EXAMPLE.COM", "hash",
            DateTimeOffset.UtcNow);
        user.PromoteToAdmin();
        var token = factory.Services.GetRequiredService<IAuthenticationTokenService>()
            .IssueForLogin(user, DateTimeOffset.UtcNow, false).AccessToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/tests/authenticated");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_token_is_rejected_by_the_configured_bearer_handler()
    {
        using var factory = new BearerAuthenticationApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/tests/authenticated");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record AuthenticationProbeResponse(string Subject);
}
