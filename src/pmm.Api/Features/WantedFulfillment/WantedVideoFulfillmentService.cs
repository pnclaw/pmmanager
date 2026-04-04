using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.DownloadClients;
using Pmm.Database;
using Pmm.Database.Enums;

namespace pmm.Api.Features.WantedFulfillment;

public class WantedVideoFulfillmentService(
    AppDbContext db,
    DownloadClientSender sender,
    ILogger<WantedVideoFulfillmentService> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);
        var preferred = settings.PreferredVideoQuality;

        var client = await db.DownloadClients
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Title)
            .FirstOrDefaultAsync(ct);

        if (client is null)
        {
            logger.LogDebug("WantedVideoFulfillmentService: no enabled download client — skipping");
            return;
        }

        // Matches for unfulfilled wanted videos where the row has not already been queued/active/completed
        var matches = await db.IndexerRowMatches
            .Include(m => m.IndexerRow)
            .Where(m => db.PrdbWantedVideos.Any(w => w.VideoId == m.PrdbVideoId && !w.IsFulfilled))
            .Where(m => !db.DownloadLogs.Any(l =>
                l.IndexerRowId == m.IndexerRowId &&
                l.Status != DownloadStatus.Failed))
            .ToListAsync(ct);

        if (matches.Count == 0)
        {
            logger.LogDebug("WantedVideoFulfillmentService: no actionable matches");
            return;
        }

        int sent = 0, failed = 0;

        foreach (var group in matches.GroupBy(m => m.PrdbVideoId))
        {
            var best = PickBest(group, preferred);
            var row  = best.IndexerRow;

            var (success, message, clientItemId) = await sender.SendAsync(
                client, row.NzbUrl, row.Title, ct);

            if (!success)
            {
                logger.LogWarning(
                    "WantedVideoFulfillmentService: failed to send '{Title}' — {Message}",
                    row.Title, message);
                failed++;
                continue;
            }

            db.DownloadLogs.Add(new DownloadLog
            {
                Id               = Guid.NewGuid(),
                IndexerRowId     = row.Id,
                DownloadClientId = client.Id,
                NzbName          = row.Title,
                NzbUrl           = row.NzbUrl,
                ClientItemId     = clientItemId,
                Status           = DownloadStatus.Queued,
            });

            logger.LogInformation(
                "WantedVideoFulfillmentService: queued '{Title}' via {Client}",
                row.Title, client.Title);

            sent++;
        }

        if (sent > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "WantedVideoFulfillmentService: {Sent} queued, {Failed} failed to send",
            sent, failed);
    }

    private static IndexerRowMatch PickBest(
        IEnumerable<IndexerRowMatch> matches, VideoQuality preferred)
    {
        var list = matches.ToList();

        // Prefer an exact quality match first
        var exact = list.FirstOrDefault(m => ParseQuality(m.IndexerRow.Title) == preferred);
        if (exact is not null)
            return exact;

        // Otherwise pick the highest quality available
        return list
            .OrderByDescending(m => (int)(ParseQuality(m.IndexerRow.Title) ?? (VideoQuality)(-1)))
            .First();
    }

    /// <summary>Parses a video quality indicator from an indexer row title.</summary>
    internal static VideoQuality? ParseQuality(string title)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("2160p") || t.Contains("4k") || t.Contains("uhd")) return VideoQuality.P2160;
        if (t.Contains("1080p") || t.Contains("1080i"))                    return VideoQuality.P1080;
        if (t.Contains("720p")  || t.Contains("720i"))                     return VideoQuality.P720;
        return null;
    }
}
