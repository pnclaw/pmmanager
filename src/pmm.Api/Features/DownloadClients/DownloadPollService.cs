using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.WantedFulfillment;
using Pmm.Database;
using Pmm.Database.Enums;

namespace pmm.Api.Features.DownloadClients;

public class DownloadPollService(
    AppDbContext db,
    SabnzbdPoller sabnzbdPoller,
    NzbgetPoller nzbgetPoller,
    IHttpClientFactory httpClientFactory,
    ILogger<DownloadPollService> logger)
{
    public async Task PollAsync(CancellationToken ct)
    {
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

            List<DownloadPollResult> results = client.ClientType switch
            {
                ClientType.Sabnzbd => await sabnzbdPoller.PollAsync(client, group, ct),
                ClientType.Nzbget  => await nzbgetPoller.PollAsync(client, group, ct),
                _                  => [],
            };

            var returnedIds = results.Select(r => r.ClientItemId).ToHashSet();

            foreach (var log in group)
            {
                if (returnedIds.Contains(log.ClientItemId!))
                {
                    log.MissedPollCount = 0;
                    ApplyResult(log, results.First(r => r.ClientItemId == log.ClientItemId));
                }
                else
                {
                    log.MissedPollCount++;
                    log.UpdatedAt = DateTime.UtcNow;

                    if (log.MissedPollCount >= 3)
                    {
                        log.Status       = DownloadStatus.Failed;
                        log.ErrorMessage = "Item not found in download client after 3 polls — likely deleted.";
                        log.CompletedAt  = DateTime.UtcNow;
                        logger.LogWarning(
                            "DownloadPollService: marking log {LogId} ('{Name}') as Failed — missing from client after 3 polls",
                            log.Id, log.NzbName);
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var completed = pendingLogs
            .Where(l => l.Status == DownloadStatus.Completed)
            .ToList();

        if (completed.Count > 0)
            await FulfillWantedVideosAsync(completed, ct);
    }

    private static void ApplyResult(DownloadLog log, DownloadPollResult result)
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

    private async Task FulfillWantedVideosAsync(List<DownloadLog> completedLogs, CancellationToken ct)
    {
        var rowIds = completedLogs.Select(l => l.IndexerRowId).ToList();

        var matches = await db.Set<IndexerRowMatch>()
            .Include(m => m.IndexerRow)
            .Where(m => rowIds.Contains(m.IndexerRowId))
            .ToListAsync(ct);

        if (matches.Count == 0) return;

        var videoIds = matches.Select(m => m.PrdbVideoId).ToList();

        var wanted = await db.PrdbWantedVideos
            .Where(w => videoIds.Contains(w.VideoId) && !w.IsFulfilled)
            .ToListAsync(ct);

        if (wanted.Count == 0) return;

        var now = DateTime.UtcNow;

        foreach (var w in wanted)
        {
            var match = matches.First(m => m.PrdbVideoId == w.VideoId);
            var log   = completedLogs.First(l => l.IndexerRowId == match.IndexerRowId);

            w.IsFulfilled           = true;
            w.FulfilledAtUtc        = now;
            w.FulfillmentExternalId = log.Id.ToString();
            w.FulfilledInQuality    = (int?)WantedVideoFulfillmentService.ParseQuality(match.IndexerRow.Title);
        }

        await db.SaveChangesAsync(ct);
        await NotifyPrdbFulfillmentAsync(wanted, ct);
    }

    private async Task NotifyPrdbFulfillmentAsync(List<PrdbWantedVideo> fulfilled, CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("DownloadPollService: PrdbApiKey not configured — skipping fulfilment notification");
            return;
        }

        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);

        foreach (var w in fulfilled)
        {
            try
            {
                var response = await http.PutAsJsonAsync(
                    $"wanted-videos/{w.VideoId}",
                    new
                    {
                        isFulfilled           = true,
                        fulfilledAtUtc        = w.FulfilledAtUtc,
                        fulfilledInQuality    = w.FulfilledInQuality,
                        fulfillmentExternalId = w.FulfillmentExternalId,
                    },
                    ct);

                if (!response.IsSuccessStatusCode)
                    logger.LogWarning(
                        "DownloadPollService: failed to notify prdb.net of fulfilment for video {VideoId} — {StatusCode}",
                        w.VideoId, response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "DownloadPollService: error notifying prdb.net of fulfilment for video {VideoId}",
                    w.VideoId);
            }
        }
    }
}
