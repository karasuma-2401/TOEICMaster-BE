using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Minio;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Profile;
using TOEICMaster.Infrastructure.Email;
using TOEICMaster.Infrastructure.Health;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Accounts;
using TOEICMaster.Infrastructure.Persistence.Exams;
using TOEICMaster.Infrastructure.Persistence.Profiles;
using TOEICMaster.Infrastructure.Repositories;
using TOEICMaster.Infrastructure.Security;
using TOEICMaster.Infrastructure.Storage;

namespace TOEICMaster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ConnectionStringsOptions>().Bind(configuration.GetSection("ConnectionStrings"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddDbContext<ToeicDbContext>((provider, options) => options.UseNpgsql(
            provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Postgres,
            provider => provider.EnableRetryOnFailure(3)));
        services.AddScoped<IAccountReader, AccountReader>();
        services.AddScoped<IExamRepository, EfExamRepository>();
        services.AddScoped<IExamMediaStore, MinioExamMediaStore>();
        services.AddScoped<IProfileRepository, EfProfileRepository>();
        services.AddScoped<IAvatarStore, MinioAvatarStore>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.InstanceName = "toeicmaster:";
        });
        services.AddOptions<RedisCacheOptions>().Configure<IOptions<ConnectionStringsOptions>>(
            (options, connections) => options.Configuration = connections.Value.Redis);
        services.AddOptions<MinioOptions>().Bind(configuration.GetSection(MinioOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IMinioClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient().WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey).WithSSL(options.UseSsl).Build();
        });
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => Enum.IsDefined(options.Security), "Invalid SMTP security mode.")
            .Validate(options => string.IsNullOrEmpty(options.Username) == string.IsNullOrEmpty(options.Password),
                "Configure both SMTP username and password, or neither.")
            .ValidateOnStart();
        services.AddTransient<IMailSender, SmtpMailSender>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.RememberMeRefreshTokenDays >= options.RefreshTokenDays,
                "Remember-me session lifetime must not be shorter than the standard session lifetime.")
            .ValidateOnStart();
        services.AddOptions<GoogleIdentityOptions>().Bind(configuration.GetSection(GoogleIdentityOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection(EmailVerificationOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });
        services.AddAuthorization();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IAuthenticationTokenService, JwtAuthenticationTokenService>();
        services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
        services.AddSingleton<IEmailVerificationCodeService, EmailVerificationCodeService>();
        services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddScoped<IUserRegistrationRepository, EfUserRegistrationRepository>();
        services.AddScoped<IAuthenticationRepository, EfAuthenticationRepository>();
        services.AddScoped<IExternalLoginRepository, EfExternalLoginRepository>();
        services.AddScoped<IPasswordResetRepository, EfPasswordResetRepository>();
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<MinioHealthCheck>("minio", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<SmtpHealthCheck>("smtp", tags: ["ready"], timeout: TimeSpan.FromSeconds(10));
        return services;
    }
}
