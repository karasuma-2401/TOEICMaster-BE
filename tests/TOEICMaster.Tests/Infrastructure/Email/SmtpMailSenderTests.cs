using System.Net;
using System.Net.Sockets;
using System.Text;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TOEICMaster.Application.Email;
using TOEICMaster.Infrastructure.Email;

namespace TOEICMaster.Tests.Infrastructure.Email;

public sealed class SmtpMailSenderTests
{
    [Fact]
    public async Task Sender_delivers_a_message_over_smtp()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new LocalSmtpServer(SmtpScenario.Success);

        await CreateSender(server).SendAsync(Message, timeout.Token).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(1, server.AttemptCount);
        Assert.Equal(1, server.AcceptedMessageCount);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(server.Message));
        using var received = await MimeMessage.LoadAsync(stream, timeout.Token);
        Assert.Equal("Practice reminder", received.Subject);
        Assert.Equal("student@toeicmaster.test", received.To.Mailboxes.Single().Address);
        Assert.Equal("noreply@toeicmaster.test", received.From.Mailboxes.Single().Address);
        Assert.NotNull(received.TextBody);
        Assert.Equal("Time to practice.", received.TextBody.TrimEnd());
    }

    [Theory]
    [InlineData(SmtpScenario.FirstGreetingTransient, 2)]
    [InlineData(SmtpScenario.FirstTwoGreetingsTransient, 3)]
    public async Task Sender_retries_transient_421_greeting_and_delivers_once(SmtpScenario scenario, int expectedAttempts)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new LocalSmtpServer(scenario);

        var error = await Record.ExceptionAsync(() =>
            CreateSender(server).SendAsync(Message, timeout.Token).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(expectedAttempts, server.AttemptCount);
        Assert.Equal(1, server.AcceptedMessageCount);
        Assert.Null(error);
    }

    [Fact]
    public async Task Sender_stops_after_three_transient_421_greeting_failures()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new LocalSmtpServer(SmtpScenario.AlwaysTransientGreeting);

        var error = await Record.ExceptionAsync(() =>
            CreateSender(server).SendAsync(Message, timeout.Token).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(3, server.AttemptCount);
        Assert.Equal(0, server.AcceptedMessageCount);
        Assert.Equal(421, (int)Assert.IsType<SmtpCommandException>(error).StatusCode);
    }

    [Theory]
    [InlineData(SmtpScenario.PermanentGreeting)]
    [InlineData(SmtpScenario.PermanentRecipient)]
    public async Task Sender_does_not_retry_permanent_550_failure(SmtpScenario scenario)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new LocalSmtpServer(scenario);

        var error = await Record.ExceptionAsync(() =>
            CreateSender(server).SendAsync(Message, timeout.Token).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(1, server.AttemptCount);
        Assert.Equal(0, server.AcceptedMessageCount);
        Assert.Equal(550, (int)Assert.IsType<SmtpCommandException>(error).StatusCode);
    }

    [Fact]
    public async Task Sender_succeeds_without_resending_when_server_accepts_data_then_drops_on_quit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new LocalSmtpServer(SmtpScenario.DropOnQuit);

        var error = await Record.ExceptionAsync(() =>
            CreateSender(server).SendAsync(Message, timeout.Token).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(1, server.AttemptCount);
        Assert.Equal(1, server.AcceptedMessageCount);
        Assert.Equal(1, server.QuitCount);
        Assert.Null(error);
    }

    private static readonly MailMessage Message =
        new("student@toeicmaster.test", "Practice reminder", "Time to practice.");

    private static SmtpMailSender CreateSender(LocalSmtpServer server) => new(Options.Create(new SmtpOptions
    {
        Host = "127.0.0.1",
        Port = server.Port,
        FromAddress = "noreply@toeicmaster.test",
        Security = MailKit.Security.SecureSocketOptions.None
    }));

    public enum SmtpScenario
    {
        Success,
        FirstGreetingTransient,
        FirstTwoGreetingsTransient,
        AlwaysTransientGreeting,
        PermanentGreeting,
        PermanentRecipient,
        DropOnQuit
    }

    private sealed class LocalSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(25));
        private readonly SmtpScenario _scenario;
        private readonly Task _run;
        private int _attempts;
        private int _accepted;
        private int _quits;

        public LocalSmtpServer(SmtpScenario scenario)
        {
            _scenario = scenario;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _run = RunAsync();
        }

        public int Port { get; }
        public int AttemptCount => Volatile.Read(ref _attempts);
        public int AcceptedMessageCount => Volatile.Read(ref _accepted);
        public int QuitCount => Volatile.Read(ref _quits);
        public string Message { get; private set; } = string.Empty;

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try
            {
                await _run.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                _stop.Dispose();
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(_stop.Token);
                    var attempt = Interlocked.Increment(ref _attempts);
                    await HandleConnectionAsync(connection, attempt, _stop.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
            catch (IOException) when (_stop.IsCancellationRequested) { }
        }

        private async Task HandleConnectionAsync(TcpClient connection, int attempt, CancellationToken cancellationToken)
        {
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            if (_scenario == SmtpScenario.AlwaysTransientGreeting
                || (_scenario == SmtpScenario.FirstGreetingTransient && attempt == 1)
                || (_scenario == SmtpScenario.FirstTwoGreetingsTransient && attempt <= 2))
            {
                await writer.WriteLineAsync("421 Service temporarily unavailable");
                return;
            }
            if (_scenario == SmtpScenario.PermanentGreeting)
            {
                await writer.WriteLineAsync("550 Service permanently unavailable");
                return;
            }

            await writer.WriteLineAsync("220 localhost SMTP");
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("RSET", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 OK");
                }
                else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync(_scenario == SmtpScenario.PermanentRecipient
                        ? "550 Recipient permanently rejected" : "250 OK");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 Send message");
                    var message = new StringBuilder();
                    string? data;
                    while ((data = await reader.ReadLineAsync(cancellationToken)) is not null && data != ".")
                        message.Append(data.StartsWith("..", StringComparison.Ordinal) ? data[1..] : data).Append("\r\n");
                    if (data is null)
                        throw new InvalidOperationException("SMTP client disconnected before completing DATA.");
                    Message = message.ToString();
                    Interlocked.Increment(ref _accepted);
                    await writer.WriteLineAsync("250 Accepted");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _quits);
                    if (_scenario != SmtpScenario.DropOnQuit)
                        await writer.WriteLineAsync("221 Bye");
                    return;
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected SMTP command: {line}");
                }
            }
        }
    }
}
