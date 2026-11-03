using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TOEICMaster.Application.Exams;

namespace TOEICMaster.Infrastructure.Storage;

public sealed class ExamMediaUploadIntentRetentionWorker(IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider, ILogger<ExamMediaUploadIntentRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ExpirePendingUploadsAsync(stoppingToken);
    }

    private async Task ExpirePendingUploadsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExamRepository>();
        var store = scope.ServiceProvider.GetRequiredService<IExamMediaStore>();
        var intents = await repository.GetExpiredPendingExamMediaUploadIntentsAsync(timeProvider.GetUtcNow(),
            cancellationToken);
        var expiredAny = false;
        foreach (var intent in intents)
        {
            try
            {
                await store.DeleteAsync(intent.ObjectKey, cancellationToken);
                intent.Expire(timeProvider.GetUtcNow());
                expiredAny = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not clean up expired exam media upload intent {IntentId}", intent.Id);
            }
        }
        if (expiredAny)
            await repository.SaveChangesAsync(cancellationToken);
    }
}
