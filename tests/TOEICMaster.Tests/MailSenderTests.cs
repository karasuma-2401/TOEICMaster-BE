using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using MimeKit;
using TOEICMaster.Application.Email;
using TOEICMaster.Infrastructure.Email;

namespace TOEICMaster.Tests;

public sealed class MailSenderTests
{
    [Fact]
    public async Task Sender_delivers_a_message_over_smtp()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var capture = CaptureMessageAsync(listener, timeout.Token);
            var sender = new SmtpMailSender(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = port
            }));
            await sender.SendAsync(new MailMessage("student@toeicmaster.test", "Practice reminder", "Time to practice."),
                timeout.Token);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(await capture));
            using var received = await MimeMessage.LoadAsync(stream, timeout.Token);
            Assert.Equal("Practice reminder", received.Subject);
            Assert.Equal("student@toeicmaster.test", received.To.Mailboxes.Single().Address);
            Assert.Equal("noreply@toeicmaster.test", received.From.Mailboxes.Single().Address);
            Assert.NotNull(received.TextBody);
            Assert.Equal("Time to practice.", received.TextBody.TrimEnd());
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> CaptureMessageAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        await writer.WriteLineAsync("220 localhost SMTP");
        var message = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 Send message");
                while (await reader.ReadLineAsync(cancellationToken) is { } data && data != ".")
                    message.Append(data.StartsWith("..", StringComparison.Ordinal) ? data[1..] : data).Append("\r\n");
                await writer.WriteLineAsync("250 Accepted");
            }
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                return message.ToString();
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
        throw new InvalidOperationException("SMTP client disconnected before QUIT.");
    }
}
