using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Authentication;

public sealed class LoginApiFactory : ApiFactory
{
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    public FakeAuthenticationRepository Repository { get; } = new();
    public FakePasswordHasher PasswordHasher { get; } = new();
    public FakeAuthenticationTokenService TokenService { get; } = new();

    public User User { get; }

    public LoginApiFactory()
    {
        User = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM",
            PasswordHasher.Hash("correct-password"), Now);
        User.VerifyEmail(Now);
        Repository.User = User;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticationRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.RemoveAll<IAuthenticationTokenService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IAuthenticationRepository>(Repository);
            services.AddSingleton<IPasswordHasher>(PasswordHasher);
            services.AddSingleton<IAuthenticationTokenService>(TokenService);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }
}

public sealed class LoginApiTests
{
    [Fact]
    public async Task Valid_credentials_return_the_token_and_user_contract()
    {
        using var factory = new LoginApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = " learner@example.com ",
            password = "correct-password",
            rememberMe = true
        });
        var body = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal("access-token", body.AccessToken);
        Assert.Equal("login-refresh-token", body.RefreshToken);
        Assert.Equal(factory.User.Id, body.User.Id);
        Assert.Equal(LoginApiFactory.Now.AddDays(30), body.RefreshTokenExpiresAtUtc);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("no-cache", response.Headers.Pragma.Select(value => value.Name));
    }

    [Theory]
    [InlineData("wrong-password", false)]
    [InlineData("correct-password", true)]
    public async Task Invalid_credentials_use_one_generic_unauthorized_response(string password,
        bool removeUser)
    {
        using var factory = new LoginApiFactory();
        if (removeUser)
            factory.Repository.User = null;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "learner@example.com",
            password
        });
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationProblem>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Email hoặc mật khẩu không chính xác.", problem?.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem?.TraceId));
        Assert.Null(factory.Repository.AddedSession);
    }

    [Fact]
    public async Task Invalid_login_input_returns_validation_problem_details()
    {
        using var factory = new LoginApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "invalid",
            password = ""
        });
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_logout_revokes_it()
    {
        using var factory = new LoginApiFactory();
        var currentSession = UserSession.Create(factory.User.Id, "old-hash", LoginApiFactory.Now,
            LoginApiFactory.Now.AddDays(1));
        factory.Repository.ActiveSession = new AuthenticationSession(currentSession, factory.User);
        using var client = factory.CreateClient();

        using var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "old-token" });
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
        using var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = refreshed?.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal("rotated-refresh-token", refreshed?.RefreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal("hash:rotated-refresh-token", factory.Repository.RevokedTokenHash);
    }

    private sealed record AuthenticationResponse(
        string TokenType,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAtUtc,
        AuthenticatedUserResponse User);

    private sealed record AuthenticatedUserResponse(Guid Id, string DisplayName, string Email);

    private sealed record AuthenticationProblem(string? Title, Dictionary<string, string[]> Errors,
        string? TraceId);
}
