using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TOEICMaster.Application.Email;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Email;

public interface IEmailOutboxProcessor
{
    Task<int> ProcessDueAsync(int batchSize, CancellationToken cancellationToken);
}

public sealed class EmailOutboxProcessor(
    ToeicDbContext dbContext,
    IMailSender mailSender,
    TimeProvider timeProvider,
    ILogger<EmailOutboxProcessor> logger) : IEmailOutboxProcessor
{
    public const int MaxAttempts = 5;

    public async Task<int> ProcessDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var dueMessages = await dbContext.EmailOutboxMessages
            .Where(message => message.Status == EmailOutboxStatus.Pending && message.NextAttemptAtUtc <= nowUtc)
            .OrderBy(message => message.NextAttemptAtUtc)
            .ThenBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var message in dueMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attemptAtUtc = timeProvider.GetUtcNow();
            try
            {
                await mailSender.SendAsync(new MailMessage(message.To, message.Subject, message.TextBody),
                    cancellationToken);
                message.MarkSent(attemptAtUtc);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var isTransient = IsTransientFailure(exception);
                var delaySeconds = Math.Min(300, 5 * Math.Pow(2, message.AttemptCount));
                var nextAttemptAtUtc = attemptAtUtc.AddSeconds(delaySeconds);
                message.RecordFailure(exception.Message, nextAttemptAtUtc, MaxAttempts, isTransient);
                logger.LogWarning(exception,
                    "Failed to deliver outbox email {MessageId} (attempt {AttemptCount}/{MaxAttempts}, transient={IsTransient}).",
                    message.Id, message.AttemptCount, MaxAttempts, isTransient);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            processed++;
        }

        return processed;
    }

    public static bool IsTransientFailure(Exception exception) => exception switch
    {
        SmtpCommandException smtp => (int)smtp.StatusCode is >= 400 and < 500,
        SmtpProtocolException => true,
        ServiceNotConnectedException => true,
        SocketException => true,
        TimeoutException => true,
        IOException => true,
        _ => false
    };
}
