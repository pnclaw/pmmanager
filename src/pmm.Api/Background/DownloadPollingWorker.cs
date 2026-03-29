using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.DownloadClients;
using Pmm.Database;
using Pmm.Database.Enums;

namespace pmm.Api.Background;

public class DownloadPollingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DownloadPollingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("DownloadPollingWorker started");

        try { await Task.Delay(InitialDelay, ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DownloadPollingWorker encountered an error");
            }

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("DownloadPollingWorker stopped");
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var poller = scope.ServiceProvider.GetRequiredService<SabnzbdPoller>();

        var pendingLogs = await db.DownloadLogs
            .Include(l => l.DownloadClient)
            .Where(l =>
                l.Status != DownloadStatus.Completed &&
                l.Status != DownloadStatus.Failed &&
                l.ClientItemId != null)
            .ToListAsync(ct);

        if (pendingLogs.Count == 0) return;

        logger.LogDebug("Polling {Count} pending download(s)", pendingLogs.Count);

        var byClient = pendingLogs.GroupBy(l => l.DownloadClientId);

        foreach (var group in byClient)
        {
            var client = group.First().DownloadClient;
            if (client.ClientType != ClientType.Sabnzbd) continue;

            var results = await poller.PollAsync(client, group, ct);

            foreach (var result in results)
            {
                var log = group.First(l => l.ClientItemId == result.ClientItemId);
                ApplyResult(log, result);
            }
        }

        await db.SaveChangesAsync(ct);

        // Mark PrdbWantedVideos as fulfilled for newly completed downloads
        var completed = pendingLogs
            .Where(l => l.Status == DownloadStatus.Completed)
            .ToList();

        if (completed.Count > 0)
            await FulfillWantedVideosAsync(db, completed, ct);
    }

    private static void ApplyResult(DownloadLog log, SabnzbdPollResult result)
    {
        log.Status         = result.Status;
        log.TotalSizeBytes = result.TotalSizeBytes ?? log.TotalSizeBytes;
        log.LastPolledAt   = DateTime.UtcNow;
        log.UpdatedAt      = DateTime.UtcNow;

        if (result.DownloadedBytes.HasValue)
            log.DownloadedBytes = result.DownloadedBytes;

        if (result.StoragePath != null)
            log.StoragePath = result.StoragePath;

        if (result.FileNames is { Count: > 0 })
            log.FileNames = JsonSerializer.Serialize(result.FileNames);

        if (result.ErrorMessage != null)
            log.ErrorMessage = result.ErrorMessage;

        if (result.Status is DownloadStatus.Completed or DownloadStatus.Failed)
            log.CompletedAt = DateTime.UtcNow;
    }

    private static async Task FulfillWantedVideosAsync(
        AppDbContext db, List<DownloadLog> completedLogs, CancellationToken ct)
    {
        var rowIds = completedLogs.Select(l => l.IndexerRowId).ToList();

        var matches = await db.Set<IndexerRowMatch>()
            .Where(m => rowIds.Contains(m.IndexerRowId))
            .ToListAsync(ct);

        if (matches.Count == 0) return;

        var videoIds = matches.Select(m => m.PrdbVideoId).ToList();

        var wanted = await db.PrdbWantedVideos
            .Where(w => videoIds.Contains(w.VideoId) && !w.IsFulfilled)
            .ToListAsync(ct);

        foreach (var w in wanted)
        {
            var match = matches.First(m => m.PrdbVideoId == w.VideoId);
            var log   = completedLogs.First(l => l.IndexerRowId == match.IndexerRowId);

            w.IsFulfilled            = true;
            w.FulfilledAtUtc         = DateTime.UtcNow;
            w.FulfillmentExternalId  = log.Id.ToString();
        }

        if (wanted.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
