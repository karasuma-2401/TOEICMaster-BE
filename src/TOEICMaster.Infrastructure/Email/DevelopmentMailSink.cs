using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Infrastructure.Email;

public sealed class DevelopmentMailSink(
    IOptions<SmtpOptions> options,
    ILogger<DevelopmentMailSink> logger,
    TimeProvider timeProvider) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Subject);
        cancellationToken.ThrowIfCancellationRequested();

        var sinkDirectory = options.Value.DevelopmentSinkDirectory;
        if (!string.IsNullOrWhiteSpace(sinkDirectory))
        {
            Directory.CreateDirectory(sinkDirectory);
            var timestamp = timeProvider.GetUtcNow().ToString("yyyyMMddHHmmssfff");
            var filePath = Path.Combine(sinkDirectory, $"{timestamp}-{Guid.NewGuid():N}.txt");
            var content = $"To: {message.To}\nSubject: {message.Subject}\n\n{message.TextBody}\n";
            await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }

        logger.LogInformation("Development mail sink captured message to {Recipient} with subject {Subject}.",
            message.To, message.Subject);
    }
}
