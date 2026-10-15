using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Authentication;

public sealed class PasswordResetApiFactory : ApiFactory
{
    public FakePasswordResetRepository Repository { get; } = new();
    public FakePasswordHasher PasswordHasher { get; } = new();
    public FakeEmailVerificationCodeService CodeService { get; } = new();
    public FakePasswordResetTokenService TokenService { get; } = new();
    public RecordingMailSender MailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.RemoveAll<IEmailVerificationCodeService>();
            services.RemoveAll<IPasswordResetTokenService>();
            services.RemoveAll<IMailSender>();
            services.AddSingleton<IPasswordResetRepository>(Repository);
            services.AddSingleton<IPasswordHasher>(PasswordHasher);
            services.AddSingleton<IEmailVerificationCodeService>(CodeService);
            services.AddSingleton<IPasswordResetTokenService>(TokenService);
            services.AddSingleton<IMailSender>(MailSender);
        });
    }
}

public sealed class PasswordResetApiTests
{
    [Fact]
    public async Task Forgot_password_is_generic_for_existing_and_missing_accounts()
    {
        using var existingFactory = new PasswordResetApiFactory();
        using var existingClient = existingFactory.CreateClient();
        using var existingResponse = await existingClient.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = " learner@example.com " });

        using var missingFactory = new PasswordResetApiFactory();
        missingFactory.Repository.HasEligibleAccount = false;
        using var missingClient = missingFactory.CreateClient();
        using var missingResponse = await missingClient.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "missing@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, existingResponse.StatusCode);
        Assert.Single(existingFactory.MailSender.Messages);
        Assert.Equal(HttpStatusCode.Accepted, missingResponse.StatusCode);
        Assert.Empty(missingFactory.MailSender.Messages);
    }

    [Fact]
    public async Task Forgot_password_validates_email_before_accessing_the_account()
    {
        using var factory = new PasswordResetApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "invalid" });
        var problem = await response.Content.ReadFromJsonAsync<PasswordResetProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Empty(factory.MailSender.Messages);
    }

    [Fact]
    public async Task Verify_password_reset_returns_a_non_cacheable_one_time_token()
    {
        using var factory = new PasswordResetApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/verify-password-reset", new
        {
            email = "learner@example.com",
            code = "123456"
        });
        var result = await response.Content.ReadFromJsonAsync<PasswordResetVerification>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reset-token", result?.ResetToken);
        Assert.NotNull(result?.ExpiresAtUtc);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Invalid_password_reset_code_returns_a_safe_bad_request()
    {
        using var factory = new PasswordResetApiFactory();
        factory.Repository.VerificationResult = PasswordResetCodeAttempt.Invalid;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/verify-password-reset", new
        {
            email = "learner@example.com",
            code = "000000"
        });
        var problem = await response.Content.ReadFromJsonAsync<PasswordResetProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mã đặt lại mật khẩu không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.",
            problem?.Title);
    }

    [Fact]
    public async Task Reset_password_accepts_the_token_and_new_password()
    {
        using var factory = new PasswordResetApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            resetToken = "reset-token",
            newPassword = "new-password"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("hash:reset-token", factory.Repository.ConsumedResetTokenHash);
        Assert.Equal("new-password", factory.PasswordHasher.Password);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Reset_password_validates_the_new_password()
    {
        using var factory = new PasswordResetApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            resetToken = "reset-token",
            newPassword = "short"
        });
        var problem = await response.Content.ReadFromJsonAsync<PasswordResetProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("NewPassword", problem.Errors.Keys);
        Assert.Null(factory.Repository.ConsumedResetTokenHash);
    }

    private sealed record PasswordResetVerification(string ResetToken, DateTimeOffset ExpiresAtUtc);
    private sealed record PasswordResetProblem(string? Title, Dictionary<string, string[]> Errors,
        string? TraceId);
}
