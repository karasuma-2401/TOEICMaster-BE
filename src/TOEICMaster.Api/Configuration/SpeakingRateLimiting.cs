using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TOEICMaster.Api.Configuration;

public static class SpeakingRateLimitPolicies
{
    public const string RecordingUpload = "speaking-recording-upload";
    public const string AnonymousSessionHeader = "X-Speaking-Anonymous-Session";
}

public static class SpeakingRateLimitingExtensions
{
    /// <summary>Recording uploads: 30 per minute per anonymous session, signed-in token or client address.</summary>
    public static IServiceCollection AddSpeakingRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options => options.AddPolicy(SpeakingRateLimitPolicies.RecordingUpload,
            context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 30,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
                })));

    // Rate limiting runs before authentication, so the key is derived from raw request headers.
    private static string PartitionKey(HttpContext context)
    {
        var anonymous = context.Request.Headers[SpeakingRateLimitPolicies.AnonymousSessionHeader].ToString();
        if (Guid.TryParse(anonymous, out var session))
            return $"anonymous:{session:N}";
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
            return $"token:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)))}";
        return $"address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client"}";
    }
}
