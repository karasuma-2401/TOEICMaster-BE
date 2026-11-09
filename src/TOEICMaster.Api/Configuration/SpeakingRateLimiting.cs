using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TOEICMaster.Api.Configuration;

public static class SpeakingRateLimitPolicies
{
    public const string RecordingUpload = "speaking-recording-upload";
    public const string EvaluationConsent = "speaking-evaluation-consent";
    public const string AnonymousSessionHeader = "X-Speaking-Anonymous-Session";
}

public static class SpeakingRateLimitingExtensions
{
    public static IServiceCollection AddSpeakingRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            // Recording uploads: 30 per minute per anonymous session, signed-in token or client address.
            options.AddPolicy(SpeakingRateLimitPolicies.RecordingUpload, context =>
                FixedWindow(PartitionKey(context), 30, TimeSpan.FromMinutes(1)));
            // AI evaluation spends provider quota. A guest can invent a new session at will, so guests share a
            // limit per client address, which behind the frontend BFF is one limit for all guests.
            options.AddPolicy(SpeakingRateLimitPolicies.EvaluationConsent, context =>
                IsGuest(context)
                    ? FixedWindow(AddressKey(context), 30, TimeSpan.FromHours(1))
                    : FixedWindow(PartitionKey(context), 10, TimeSpan.FromMinutes(1)));
        });

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = window
        });

    private static bool IsGuest(HttpContext context) =>
        Guid.TryParse(context.Request.Headers[SpeakingRateLimitPolicies.AnonymousSessionHeader].ToString(), out _);

    // Rate limiting runs before authentication, so the key is derived from raw request headers.
    private static string PartitionKey(HttpContext context)
    {
        var anonymous = context.Request.Headers[SpeakingRateLimitPolicies.AnonymousSessionHeader].ToString();
        if (Guid.TryParse(anonymous, out var session))
            return $"anonymous:{session:N}";
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
            return $"token:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)))}";
        return AddressKey(context);
    }

    private static string AddressKey(HttpContext context) =>
        $"address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client"}";
}
