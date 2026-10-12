using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Email;
using TOEICMaster.Infrastructure.Email;
using TOEICMaster.Infrastructure.Health;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Accounts;
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
        // Authentication services
        services.AddSingleton<TOEICMaster.Application.Common.Security.IPasswordHasher, TOEICMaster.Infrastructure.Security.Pbkdf2PasswordHasher>();
        services.AddScoped<TOEICMaster.Application.Common.Interfaces.IUserRepository, TOEICMaster.Infrastructure.Repositories.EfUserRepository>();
        services.Configure<TOEICMaster.Infrastructure.Security.JwtOptions>(configuration.GetSection(TOEICMaster.Infrastructure.Security.JwtOptions.SectionName));
        services.AddSingleton<TOEICMaster.Application.Common.Security.ITokenService, TOEICMaster.Infrastructure.Security.JwtTokenService>();
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<MinioHealthCheck>("minio", tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<SmtpHealthCheck>("smtp", tags: ["ready"], timeout: TimeSpan.FromSeconds(10));
        return services;
    }
}
