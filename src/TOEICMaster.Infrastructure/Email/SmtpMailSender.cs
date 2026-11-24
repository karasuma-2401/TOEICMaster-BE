using System.Net.Sockets;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Infrastructure.Email;

public sealed class SmtpMailSender(IOptions<SmtpOptions> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Subject);
        var settings = options.Value;
        using var email = new MimeMessage();
        email.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;
        email.Body = new TextPart("plain") { Text = message.TextBody };

        for (var attempt = 0; ; attempt++)
        {
            using var client = new SmtpClient { Timeout = 10000 };
            var submitting = false;
            try
            {
                await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
                if (!string.IsNullOrEmpty(settings.Username))
                    await client.AuthenticateAsync(settings.Username, settings.Password!, cancellationToken);
                submitting = true;
                await client.SendAsync(email, cancellationToken);
                try
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or SocketException or
                    SmtpProtocolException or SmtpCommandException or OperationCanceledException)
                {
                    // SMTP already accepted DATA; a failed QUIT must not cause a duplicate email.
                }
                return;
            }
            catch (Exception exception) when (attempt < 2 &&
                (exception is SmtpCommandException smtp && (int)smtp.StatusCode is >= 400 and < 500 ||
                 !submitting && exception is IOException or SocketException or SmtpProtocolException))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
            }
        }
    }
}
