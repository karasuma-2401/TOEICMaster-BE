using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TOEICMaster.Application.Writing.Evaluations;

namespace TOEICMaster.Infrastructure.Writing.Evaluations;

public sealed class WritingEvaluationWorker(IServiceScopeFactory scopeFactory,
    ILogger<WritingEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWritingEvaluationProcessor>();
                var processed = await processor.ProcessNextAsync(stoppingToken);
                await Task.Delay(processed ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Writing evaluation worker could not process a job.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
