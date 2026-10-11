using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TOEICMaster.Api.ExceptionHandling;
using TOEICMaster.Application;
using TOEICMaster.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler(options => options.StatusCodeSelector = _ => StatusCodes.Status500InternalServerError);
builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"],
    builder.Configuration["AutoMapper:LicenseKey"], typeof(Program).Assembly);
builder.Services.AddInfrastructure(builder.Configuration);
// Configure JWT authentication if present
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<TOEICMaster.Infrastructure.Security.JwtOptions>();
if (jwtOptions is null || string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException("JWT configuration is missing or invalid. Configure Jwt:Issuer, Jwt:Audience and a strong Jwt:Key (at least 32 characters).");
}
builder.Services.Configure<TOEICMaster.Infrastructure.Security.JwtOptions>(jwtSection);
var signingKey = Encoding.UTF8.GetBytes(jwtOptions.Key);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(signingKey),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program;
