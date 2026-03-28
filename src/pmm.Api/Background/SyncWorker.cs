using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.Prdb.Sync;
using Pmm.Database;

namespace pmm.Api.Background;

public class SyncWorker(IServiceScopeFactory scopeFactory, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("SyncWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SyncWorker encountered an error");
            }

            await Task.Delay(Interval, ct).ConfigureAwait(false);
        }

        logger.LogInformation("SyncWorker stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        logger.LogInformation("SyncWorker run started at {Time}", DateTimeOffset.UtcNow);

        var actorSync = scope.ServiceProvider.GetRequiredService<PrdbActorSyncService>();
        await actorSync.RunAsync(ct);

        var videoDetailSync = scope.ServiceProvider.GetRequiredService<PrdbVideoDetailSyncService>();
        await videoDetailSync.RunAsync(ct);

        var wantedVideoSync = scope.ServiceProvider.GetRequiredService<PrdbWantedVideoSyncService>();
        await wantedVideoSync.RunAsync(ct);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.AppSettings.FirstAsync(ct);
        settings.SyncWorkerLastRunAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("SyncWorker run completed at {Time}", DateTimeOffset.UtcNow);
    }
}
