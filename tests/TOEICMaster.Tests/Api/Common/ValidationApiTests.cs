using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio.Exceptions;
using Npgsql;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Email.Commands.SendEmail;
using TOEICMaster.Application.ExerciseBank;
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

    [HttpGet("minio-not-found")]
    public IActionResult MinioNotFound() => throw new ObjectNotFoundException("missing.png", "Object missing");

    [HttpGet("lr-conflict")]
    public IActionResult LrConflict() => throw new LrExerciseBankConflictException("Duplicate exercise code.");

    [HttpGet("postgres-error/{sqlState}")]
    public IActionResult PostgresError(string sqlState) =>
        throw new DbUpdateException("sensitive SQL table constraint name",
            new PostgresException("sensitive column detail", "ERROR", "ERROR", sqlState));
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

    [Fact]
    public async Task Global_exception_handler_maps_minio_404_lr_conflict_type_and_postgres_errors_safely_with_traceId()
    {
        using var factory = new ValidationApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using var minioResponse = await client.GetAsync("/tests/validation/minio-not-found");
        Assert.Equal(HttpStatusCode.NotFound, minioResponse.StatusCode);
        Assert.Equal("application/problem+json", minioResponse.Content.Headers.ContentType?.MediaType);
        var minioProblem = await minioResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(minioProblem.GetProperty("traceId").GetString()));

        using var conflictResponse = await client.GetAsync("/tests/validation/lr-conflict");
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflictProblem = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.10", conflictProblem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(conflictProblem.GetProperty("traceId").GetString()));

        foreach (var (sqlState, expectedStatus) in new[]
                 {
                     (PostgresErrorCodes.UniqueViolation, HttpStatusCode.Conflict),
                     (PostgresErrorCodes.StringDataRightTruncation, HttpStatusCode.BadRequest),
                     (PostgresErrorCodes.ForeignKeyViolation, HttpStatusCode.Conflict)
                 })
        {
            using var pgResponse = await client.GetAsync($"/tests/validation/postgres-error/{sqlState}");
            Assert.Equal(expectedStatus, pgResponse.StatusCode);
            var body = await pgResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("sensitive", body, StringComparison.OrdinalIgnoreCase);
            var pgProblem = JsonDocument.Parse(body).RootElement;
            Assert.False(string.IsNullOrEmpty(pgProblem.GetProperty("traceId").GetString()));
        }
    }
}
