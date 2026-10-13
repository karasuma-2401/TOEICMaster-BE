using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Tests;

public sealed class RegistrationApiFactory : ApiFactory
{
    public FakeUserRegistrationRepository Repository { get; } = new();
    public FakePasswordHasher PasswordHasher { get; } = new();
    public FakeEmailVerificationCodeService CodeService { get; } = new();
    public RecordingMailSender MailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRegistrationRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.RemoveAll<IEmailVerificationCodeService>();
            services.RemoveAll<IMailSender>();
            services.AddSingleton<IUserRegistrationRepository>(Repository);
            services.AddSingleton<IPasswordHasher>(PasswordHasher);
            services.AddSingleton<IEmailVerificationCodeService>(CodeService);
            services.AddSingleton<IMailSender>(MailSender);
        });
    }
}

public sealed class RegistrationApiTests
{
    [Fact]
    public async Task Valid_request_matches_the_frontend_contract()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "  Nguyen Van A  ",
            email = "  learner@example.com  ",
            password = " password "
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
        Assert.Equal("Nguyen Van A", factory.Repository.AddedUser?.DisplayName);
        Assert.Equal(" password ", factory.PasswordHasher.Password);
        Assert.Single(factory.MailSender.Messages);
    }

    [Fact]
    public async Task Invalid_request_returns_validation_problem_details()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "",
            email = "invalid",
            password = "short"
        }, CancellationToken.None);
        var problem = await response.Content.ReadFromJsonAsync<RegistrationProblem>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Contains("DisplayName", problem.Errors.Keys);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.Null(factory.Repository.AddedUser);
    }

    [Fact]
    public async Task Validation_applies_length_limits_after_trimming()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = $"  {new string('a', 100)}  ",
            email = "  learner@example.com  ",
            password = "password"
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(100, factory.Repository.AddedUser?.DisplayName.Length);
    }

    [Fact]
    public async Task Duplicate_email_returns_the_conflict_expected_by_the_frontend()
    {
        using var factory = new RegistrationApiFactory();
        factory.Repository.ExistingEmail = true;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "Learner",
            email = "learner@example.com",
            password = "password"
        }, CancellationToken.None);
        var problem = await response.Content.ReadFromJsonAsync<RegistrationProblem>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email này đã được sử dụng.", problem?.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem?.TraceId));
    }

    [Fact]
    public async Task Verification_activates_the_account_and_returns_no_content()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            email = " learner@example.com ",
            code = "123456"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("LEARNER@EXAMPLE.COM", factory.Repository.VerifiedEmail);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", factory.Repository.VerifiedCodeHash);
    }

    [Fact]
    public async Task Invalid_or_exhausted_verification_code_returns_a_safe_bad_request()
    {
        using var factory = new RegistrationApiFactory();
        factory.Repository.VerificationResult = EmailVerificationAttempt.Invalid;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            email = "learner@example.com",
            code = "000000"
        });
        var problem = await response.Content.ReadFromJsonAsync<RegistrationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mã xác minh không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.", problem?.Title);
    }

    [Fact]
    public async Task Resend_is_generic_and_sends_only_for_an_eligible_pending_account()
    {
        using var pendingFactory = new RegistrationApiFactory();
        pendingFactory.Repository.ExistingPendingEmail = true;
        using var pendingClient = pendingFactory.CreateClient();
        using var pendingResponse = await pendingClient.PostAsJsonAsync("/api/v1/auth/resend-verification",
            new { email = "learner@example.com" });

        using var missingFactory = new RegistrationApiFactory();
        using var missingClient = missingFactory.CreateClient();
        using var missingResponse = await missingClient.PostAsJsonAsync("/api/v1/auth/resend-verification",
            new { email = "missing@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, pendingResponse.StatusCode);
        Assert.Single(pendingFactory.MailSender.Messages);
        Assert.Equal(HttpStatusCode.Accepted, missingResponse.StatusCode);
        Assert.Empty(missingFactory.MailSender.Messages);
    }

    private sealed record RegistrationProblem(string? Title, Dictionary<string, string[]> Errors,
        string? TraceId);
}
