using TOEICMaster.Application.Email;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class RecordingMailSender : IMailSender
{
    public List<MailMessage> Messages { get; } = [];
    public CancellationToken LastCancellationToken { get; private set; }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Messages.Add(message);
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}
