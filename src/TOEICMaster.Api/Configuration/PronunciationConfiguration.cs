using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TOEICMaster.Infrastructure.Pronunciation;

namespace TOEICMaster.Api.Configuration;

public static class PronunciationRateLimitPolicies
{
    public const string Assessment = "pronunciation-assessment";
}

public static class PronunciationConfigurationExtensions
{
    /// <summary>Everything the IPA pronunciation feature needs, registered from one place.</summary>
    public static IServiceCollection AddPronunciation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPronunciationAssessment(configuration);
        // Each assessment spends Azure quota: 30 per minute per signed-in token (or client address).
        services.AddRateLimiter(options => options.AddPolicy(PronunciationRateLimitPolicies.Assessment, context =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            })));
        return services;
    }

    // Rate limiting runs before authentication, so the key comes from the raw Authorization header.
    private static string PartitionKey(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        return string.IsNullOrEmpty(authorization)
            ? $"pronunciation-address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client"}"
            : $"pronunciation-token:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)))}";
    }
}
