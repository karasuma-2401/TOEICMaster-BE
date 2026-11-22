using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;

namespace TOEICMaster.Api.Configuration;

public sealed class ForwardedHeadersSettings
{
    public bool Enabled { get; set; } = true;
    public int? ForwardLimit { get; set; } = 1;
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}

public static class ForwardedHeadersConfigurationExtensions
{
    public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = settings.Enabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = settings.ForwardLimit is > 0 ? settings.ForwardLimit.Value : 1;

            foreach (var proxy in settings.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                    options.KnownProxies.Add(address);
            }

            foreach (var network in settings.KnownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsedNetwork))
                    options.KnownIPNetworks.Add(parsedNetwork);
            }
        });

        return services;
    }

    public static IApplicationBuilder UseAuthLoginRateLimitContext(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method) &&
                context.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                await TryExtractLoginEmailAsync(context);
            }

            await next();
        });

    private static async Task TryExtractLoginEmailAsync(HttpContext context)
    {
        const int maxBodyBytes = 4096;
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType) ||
            !string.Equals(mediaType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.Request.ContentLength is > maxBodyBytes)
            return;

        context.Request.EnableBuffering();
        var buffer = new byte[maxBodyBytes + 1];
        var totalRead = 0;
        try
        {
            while (totalRead < buffer.Length)
            {
                var read = await context.Request.Body.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead),
                    context.RequestAborted);
                if (read == 0)
                    break;
                totalRead += read;
            }
        }
        finally
        {
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
        }

        if (totalRead is 0 or > maxBodyBytes)
            return;

        try
        {
            using var document = JsonDocument.Parse(buffer.AsMemory(0, totalRead));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "email", StringComparison.OrdinalIgnoreCase) ||
                    property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var rawEmail = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(rawEmail))
                    context.Items[AuthRateLimitContext.LoginEmailItemKey] = rawEmail.Trim().ToUpperInvariant();
                return;
            }
        }
        catch (JsonException)
        {
            // Fall back to IP-only rate limit partitioning when payload is malformed JSON.
        }
    }
}
