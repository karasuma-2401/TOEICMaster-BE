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
                context => CreatePartition(context, permitLimit: 5));
            options.AddPolicy(AuthRateLimitPolicies.Credentials,
                context => CreatePartition(context, permitLimit: 10));
            options.AddPolicy(AuthRateLimitPolicies.Sessions,
                context => CreatePartition(context, permitLimit: 30));
        });
        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
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
            Title = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}
