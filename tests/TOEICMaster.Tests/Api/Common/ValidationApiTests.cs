using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Email.Commands.SendEmail;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Common;

public sealed class ValidationApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ValidationProbeController).Assembly);
            services.AddSingleton<RecordingMailSender>();
            services.AddSingleton<IMailSender>(provider => provider.GetRequiredService<RecordingMailSender>());
        });
    }
}

[ApiController]
[Route("tests/validation")]
public sealed class ValidationProbeController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send(SendEmailCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("unexpected-error")]
    public IActionResult UnexpectedError() => throw new InvalidOperationException("Private internal error details.");
}

public sealed class ValidationApiTests
{
    [Fact]
    public async Task Validation_failure_returns_problem_details_with_property_errors()
    {
        using var factory = new ValidationApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/tests/validation", new SendEmailCommand("invalid", " ", ""));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("To", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("Subject", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("TextBody", out _));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.Empty(factory.Services.GetRequiredService<RecordingMailSender>().Messages);
    }

    [Fact]
    public async Task Validation_problem_is_also_returned_for_unsupported_accept_header()
    {
        using var factory = new ValidationApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        using var response = await client.PostAsJsonAsync("/tests/validation", new SendEmailCommand("invalid", "Subject", "Body"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Unexpected_errors_keep_the_generic_500_response_without_exposing_details()
    {
        using var factory = new ValidationApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/tests/validation/unexpected-error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private internal error details", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void All_registered_mapping_profiles_are_valid()
    {
        using var factory = new ApiFactory();
        factory.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }
}
