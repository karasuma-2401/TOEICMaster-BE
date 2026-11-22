using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace TOEICMaster.Tests.Api.Authentication;

public sealed class RateLimitingApiTests
{
    [Fact]
    public async Task Register_stops_before_sending_mail_when_email_rate_limit_is_exceeded()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var allowed = await RegisterAsync(client, $"learner{attempt}@example.com");
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var rejected = await RegisterAsync(client, "blocked@example.com");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(5, factory.MailSender.Messages.Count);
    }

    [Fact]
    public async Task Login_returns_problem_details_and_retry_after_when_rate_limit_is_exceeded()
    {
        using var factory = new LoginApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var allowed = await LoginAsync(client);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var rejected = await LoginAsync(client);
        var problem = await rejected.Content.ReadFromJsonAsync<RateLimitProblem>();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.", problem?.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem?.TraceId));
    }

    [Fact]
    public async Task Forgot_password_stops_before_sending_mail_when_email_rate_limit_is_exceeded()
    {
        using var factory = new PasswordResetApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var allowed = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new { email = "learner@example.com" });
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "learner@example.com" });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(5, factory.MailSender.Messages.Count);
    }

    [Fact]
    public async Task Login_rate_limit_isolates_distinct_emails_from_the_same_ip()
    {
        using var factory = new LoginApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var allowed = await LoginAsync(client, "victim@example.com");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var blockedVictim = await LoginAsync(client, "  VICTIM@example.com ");
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedVictim.StatusCode);

        using var unaffectedUser = await LoginAsync(client, "other-learner@example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, unaffectedUser.StatusCode);
    }

    [Fact]
    public async Task Forwarded_for_from_untrusted_peer_is_ignored_while_trusted_loopback_proxy_is_applied()
    {
        using var untrustedFactory = new LoginApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PeerIpStartupFilter(IPAddress.Parse("198.51.100.1")))));
        using var client = untrustedFactory.CreateClient();

        // Untrusted direct peer (198.51.100.1 is not in KnownProxies) cannot spoof X-Forwarded-For.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email = "spoof@example.com", password = "wrong-password" })
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{attempt + 1}");
            using var allowed = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var spoofedNext = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "spoof@example.com", password = "wrong-password" })
        };
        spoofedNext.Headers.Add("X-Forwarded-For", "203.0.113.99");
        using var blocked = await client.SendAsync(spoofedNext);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // Trusted loopback proxy (127.0.0.1) applies X-Forwarded-For so distinct client IPs are partitioned separately.
        using var trustedFactory = new LoginApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PeerIpStartupFilter(IPAddress.Loopback))));
        using var trustedClient = trustedFactory.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email = "proxied@example.com", password = "wrong-password" })
            };
            req.Headers.Add("X-Forwarded-For", "198.51.100.10");
            using var res = await trustedClient.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        using var otherClientReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "proxied@example.com", password = "wrong-password" })
        };
        otherClientReq.Headers.Add("X-Forwarded-For", "198.51.100.11");
        using var otherClientRes = await trustedClient.SendAsync(otherClientReq);
        Assert.Equal(HttpStatusCode.Unauthorized, otherClientRes.StatusCode);
    }

    private sealed class PeerIpStartupFilter(IPAddress peerIp) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = peerIp;
                return nextMiddleware();
            });
            next(app);
        };
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email = "learner@example.com") =>
        client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "wrong-password"
        });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "Learner",
            email,
            password = "password"
        });

    private sealed record RateLimitProblem(string? Title, string? TraceId);
}
