using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
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

    private static string PartitionKey(HttpContext context)
    {
        if (context.User.Identity is ClaimsIdentity { IsAuthenticated: true } identity &&
            Guid.TryParse(identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                          identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return $"pronunciation-user:{userId:D}";

        return $"pronunciation-address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client"}";
    }
}
