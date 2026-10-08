using MailKit.Net.Smtp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TOEICMaster.Infrastructure.Email;

namespace TOEICMaster.Infrastructure.Health;

public sealed class SmtpHealthCheck(IOptions<SmtpOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var client = new SmtpClient { Timeout = 5000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password!, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
