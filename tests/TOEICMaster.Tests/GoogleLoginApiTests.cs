using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests;

public sealed class GoogleLoginApiFactory : ApiFactory
{
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 1, 0, 0, TimeSpan.Zero);

    public FakeGoogleIdentityVerifier Verifier { get; } = new();
    public FakeExternalLoginRepository ExternalRepository { get; } = new();
    public FakeAuthenticationRepository AuthenticationRepository { get; } = new();
    public FakeAuthenticationTokenService TokenService { get; } = new();
    public User User { get; }

    public GoogleLoginApiFactory()
    {
        User = User.CreateExternal("Google Learner", "learner@gmail.com", "LEARNER@GMAIL.COM", Now);
        ExternalRepository.Resolution = ExternalLoginResolution.Resolved(User);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGoogleIdentityVerifier>();
            services.RemoveAll<IExternalLoginRepository>();
            services.RemoveAll<IAuthenticationRepository>();
            services.RemoveAll<IAuthenticationTokenService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IGoogleIdentityVerifier>(Verifier);
            services.AddSingleton<IExternalLoginRepository>(ExternalRepository);
            services.AddSingleton<IAuthenticationRepository>(AuthenticationRepository);
            services.AddSingleton<IAuthenticationTokenService>(TokenService);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }
}

public sealed class GoogleLoginApiTests
{
    [Fact]
    public async Task Verified_credential_returns_the_standard_authentication_contract()
    {
        using var factory = new GoogleLoginApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google", new
        {
            credential = "signed-google-id-token",
            rememberMe = true
        });
        var body = await response.Content.ReadFromJsonAsync<GoogleAuthenticationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", body?.TokenType);
        Assert.Equal(factory.User.Id, body?.User.Id);
        Assert.Equal("signed-google-id-token", factory.Verifier.Credential);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Invalid_google_credential_returns_safe_unauthorized_problem_details()
    {
        using var factory = new GoogleLoginApiFactory();
        factory.Verifier.Exception = new InvalidGoogleCredentialException();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google",
            new { credential = "invalid-token" });
        var problem = await response.Content.ReadFromJsonAsync<GoogleAuthenticationProblem>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Google credential không hợp lệ hoặc đã hết hạn.", problem?.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem?.TraceId));
    }

    [Fact]
    public async Task Unsafe_automatic_link_returns_conflict()
    {
        using var factory = new GoogleLoginApiFactory();
        factory.ExternalRepository.Resolution = ExternalLoginResolution.LinkRequired();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google",
            new { credential = "signed-google-id-token" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Missing_credential_returns_validation_problem_details()
    {
        using var factory = new GoogleLoginApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google", new { credential = "" });
        var problem = await response.Content.ReadFromJsonAsync<GoogleAuthenticationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Credential", problem.Errors.Keys);
    }

    private sealed record GoogleAuthenticationResponse(string TokenType, GoogleAuthenticatedUser User);
    private sealed record GoogleAuthenticatedUser(Guid Id, string DisplayName, string Email);
    private sealed record GoogleAuthenticationProblem(string? Title, Dictionary<string, string[]> Errors,
        string? TraceId);
}
