using System.Net;
using System.Net.Http.Json;

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

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "learner@example.com",
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
