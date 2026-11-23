using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TOEICMaster.Api.Configuration;

public static class AuthRateLimitPolicies
{
    public const string EmailDelivery = "auth-email-delivery";
    public const string Credentials = "auth-credentials";
    public const string Sessions = "auth-sessions";
}

internal static class AuthRateLimitContext
{
    public const string LoginEmailItemKey = "AuthRateLimit:LoginEmail";
}

public static class AuthRateLimitingExtensions
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
            options.AddPolicy(AuthRateLimitPolicies.EmailDelivery,
                context => CreatePartition(ClientIpKey(context), permitLimit: 5));
            options.AddPolicy(AuthRateLimitPolicies.Credentials,
                context => CreatePartition(CredentialsPartitionKey(context), permitLimit: 10));
            options.AddPolicy(AuthRateLimitPolicies.Sessions,
                context => CreatePartition(ClientIpKey(context), permitLimit: 30));
        });
        return services;
    }

    private static string ClientIpKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";

    private static string CredentialsPartitionKey(HttpContext context)
    {
        var clientIp = ClientIpKey(context);
        return context.Items.TryGetValue(AuthRateLimitContext.LoginEmailItemKey, out var value) &&
               value is string normalizedEmail &&
               normalizedEmail.Length > 0
            ? $"{clientIp}|{normalizedEmail}"
            : clientIp;
    }

    private static RateLimitPartition<string> CreatePartition(string partitionKey, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = Window
            });

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests. Please try again later.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}
