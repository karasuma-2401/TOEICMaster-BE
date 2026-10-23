using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Speaking;

/// <summary>Deletes recordings of anonymous (guest) attempts 30 days after the attempt started.</summary>
public sealed class SpeakingRecordingRetentionWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<SpeakingRecordingRetentionWorker> logger) : BackgroundService
{
    public static readonly TimeSpan GuestRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(FirstRunDelay, timeProvider, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            if (stoppingToken.IsCancellationRequested)
                return;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var removed = await PurgeExpiredGuestRecordingsAsync(
                    scope.ServiceProvider.GetRequiredService<ToeicDbContext>(),
                    scope.ServiceProvider.GetRequiredService<ISpeakingMediaStore>(),
                    timeProvider.GetUtcNow(), stoppingToken);
                if (removed > 0)
                    logger.LogInformation("Removed {Count} expired guest Speaking recordings.", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Speaking recording retention cleanup failed; it will retry later.");
            }
            await Task.Delay(Interval - FirstRunDelay, timeProvider, stoppingToken)
                .ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    public static async Task<int> PurgeExpiredGuestRecordingsAsync(ToeicDbContext context, ISpeakingMediaStore store,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - GuestRetention;
        var removed = 0;
        while (true)
        {
            var questions = await (from question in context.SpeakingAttemptQuestions
                                   join attempt in context.SpeakingAttempts on question.AttemptId equals attempt.Id
                                   where attempt.AnonymousSessionHash != null && attempt.CreatedAtUtc < cutoff &&
                                         question.RecordingAssetId != null
                                   select question).Take(BatchSize).ToListAsync(cancellationToken);
            if (questions.Count == 0)
                return removed;
            var assetIds = questions.Select(question => question.RemoveRecording()!.Value).ToArray();
            var assets = await context.SpeakingMediaAssets.Where(asset => assetIds.Contains(asset.Id))
                .ToListAsync(cancellationToken);
            context.SpeakingMediaAssets.RemoveRange(assets);
            await context.SaveChangesAsync(cancellationToken);
            foreach (var asset in assets)
            {
                try { await store.DeleteAsync(SpeakingMediaKind.Recording, asset.ObjectKey, cancellationToken); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The database no longer references the object, so a leftover file is never served.
                }
            }
            removed += questions.Count;
            context.ChangeTracker.Clear();
        }
    }
}
