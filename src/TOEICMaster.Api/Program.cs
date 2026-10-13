using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Api.ExceptionHandling;
using TOEICMaster.Application;
using TOEICMaster.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var dotEnvPath = DotEnvConfiguration.FindFile(Directory.GetCurrentDirectory(), builder.Environment.ContentRootPath);
if (dotEnvPath is not null)
{
    builder.Configuration.AddToeicMasterDotEnvFile(dotEnvPath);
    builder.Configuration.AddEnvironmentVariables();
    if (args.Length > 0)
        builder.Configuration.AddCommandLine(args);
}
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthRateLimiting();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DuplicateEmailExceptionHandler>();
builder.Services.AddExceptionHandler<AuthenticationExceptionHandler>();
builder.Services.AddExceptionHandler(options => options.StatusCodeSelector = _ => StatusCodes.Status500InternalServerError);
builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"],
    builder.Configuration["AutoMapper:LicenseKey"], typeof(Program).Assembly);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("TOEICMaster API")
        .DisableAgent());
}
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program;
