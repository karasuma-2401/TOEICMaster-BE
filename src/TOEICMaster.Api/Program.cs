using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Api.ExceptionHandling;
using TOEICMaster.Application;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Infrastructure;

var runAdminSeed = args.Any(argument => argument.Equals("--seed-admin", StringComparison.OrdinalIgnoreCase));
var configurationArgs = args.Where(argument => !argument.Equals("--seed-admin", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var builder = WebApplication.CreateBuilder(configurationArgs);
var dotEnvPath = DotEnvConfiguration.FindFile(Directory.GetCurrentDirectory(), builder.Environment.ContentRootPath);
if (dotEnvPath is not null)
{
    builder.Configuration.AddToeicMasterDotEnvFile(dotEnvPath);
}
builder.Configuration.AddEnvironmentVariables();
if (configurationArgs.Length > 0)
    builder.Configuration.AddCommandLine(configurationArgs);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthRateLimiting();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DuplicateEmailExceptionHandler>();
builder.Services.AddExceptionHandler<AuthenticationExceptionHandler>();
builder.Services.AddExceptionHandler<WritingAdministrationExceptionHandler>();
builder.Services.AddExceptionHandler<WritingAttemptExceptionHandler>();
builder.Services.AddExceptionHandler(options => options.StatusCodeSelector = _ => StatusCodes.Status500InternalServerError);
builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"],
    builder.Configuration["AutoMapper:LicenseKey"], typeof(Program).Assembly);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization(options => options.AddPolicy("AccountAdministration",
    policy => policy.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddAuthorization(options => options.AddPolicy("WritingAdministration",
    policy => policy.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        return Task.CompletedTask;
    });
    return next(context);
});
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

if (runAdminSeed)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("--seed-admin chỉ được phép trong môi trường Development.");

    await using var scope = app.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<IAdminBootstrapSeeder>()
        .SeedAsync(CancellationToken.None);
    app.Logger.LogInformation("Admin bootstrap completed with action {Action}.", result.Action);
    return;
}

app.Run();

public partial class Program;
