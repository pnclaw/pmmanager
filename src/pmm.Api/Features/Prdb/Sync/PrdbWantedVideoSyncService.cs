using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb.Sync;

public class PrdbWantedVideoSyncService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<PrdbWantedVideoSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int PageSize           = 500;
    private const int MaxStubsPerRun    = 50; // max GET /videos/{id} calls per run

    public async Task RunAsync(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("PrdbWantedVideoSyncService: PrdbApiKey not configured — skipping");
            return;
        }

        var http = CreateClient(settings);

        // Phase 1: Fetch all wanted videos from the API
        var allWanted = await FetchAllWantedVideosAsync(http, ct);
        var allVideoIds = allWanted.Select(w => w.VideoId).ToHashSet();

        logger.LogInformation("PrdbWantedVideoSyncService: fetched {Count} wanted videos from API", allWanted.Count);

        // Phase 2: Ensure PrdbVideo (and site) stubs exist for any unknown video IDs
        await EnsureVideoStubsAsync(http, allVideoIds, ct);

        // Phase 3: Upsert wanted video entries — only for videos that now exist in our DB
        var knownVideoIds = await db.PrdbVideos
            .Where(v => allVideoIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToHashSetAsync(ct);

        var toUpsert = allWanted.Where(w => knownVideoIds.Contains(w.VideoId)).ToList();
        var upserted = await UpsertWantedVideosAsync(toUpsert, ct);

        // Phase 4: Delete entries no longer present on the wanted list
        var deleted = await DeleteRemovedEntriesAsync(allVideoIds, ct);

        settings.PrdbWantedVideoLastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var skipped = allWanted.Count - knownVideoIds.Count;
        logger.LogInformation(
            "PrdbWantedVideoSyncService: {Upserted} upserted, {Deleted} deleted, {Skipped} skipped (video stub pending next run)",
            upserted, deleted, skipped);
    }

    // ── Phase 1: Fetch all pages ─────────────────────────────────────────────

    private async Task<List<PrdbApiWantedVideoSummary>> FetchAllWantedVideosAsync(HttpClient http, CancellationToken ct)
    {
        var all  = new List<PrdbApiWantedVideoSummary>();
        var page = 1;

        while (true)
        {
            var response = await http.GetFromJsonAsync<PrdbApiPagedResult<PrdbApiWantedVideoSummary>>(
                $"wanted-videos?Page={page}&PageSize={PageSize}", JsonOptions, ct);

            if (response is null || response.Items.Count == 0) break;

            all.AddRange(response.Items);

            if (all.Count >= response.TotalCount) break;

            page++;
        }

        return all;
    }

    // ── Phase 2: Stub creation for unknown videos ────────────────────────────

    private async Task EnsureVideoStubsAsync(HttpClient http, HashSet<Guid> videoIds, CancellationToken ct)
    {
        var existingVideoIds = await db.PrdbVideos
            .Where(v => videoIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToHashSetAsync(ct);

        var missingIds = videoIds.Except(existingVideoIds).Take(MaxStubsPerRun).ToList();

        if (missingIds.Count == 0) return;

        logger.LogInformation(
            "PrdbWantedVideoSyncService: fetching details for {Count} video(s) not yet in DB",
            missingIds.Count);

        var existingSiteIds = await db.PrdbSites
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var videoId in missingIds)
        {
            ct.ThrowIfCancellationRequested();

            var detail = await http.GetFromJsonAsync<PrdbApiVideoDetail>(
                $"videos/{videoId}", JsonOptions, ct);

            if (detail is null)
            {
                logger.LogWarning("PrdbWantedVideoSyncService: no detail returned for video {VideoId}", videoId);
                continue;
            }

            // Ensure site stub exists
            if (!existingSiteIds.Contains(detail.Site.Id))
            {
                db.PrdbSites.Add(new PrdbSite
                {
                    Id          = detail.Site.Id,
                    Title       = detail.Site.Title,
                    Url         = detail.Site.Url,
                    SyncedAtUtc = now,
                });
                existingSiteIds.Add(detail.Site.Id);
            }

            // Create video stub — DetailSyncedAtUtc left null so PrdbVideoDetailSyncService fills it in
            db.PrdbVideos.Add(new PrdbVideo
            {
                Id               = detail.Id,
                Title            = detail.Title,
                ReleaseDate      = detail.ReleaseDate,
                SiteId           = detail.Site.Id,
                PrdbCreatedAtUtc = detail.CreatedAtUtc,
                PrdbUpdatedAtUtc = detail.UpdatedAtUtc,
                SyncedAtUtc      = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Phase 3: Upsert ──────────────────────────────────────────────────────

    private async Task<int> UpsertWantedVideosAsync(List<PrdbApiWantedVideoSummary> items, CancellationToken ct)
    {
        if (items.Count == 0) return 0;

        var incomingIds = items.Select(w => w.VideoId).ToList();
        var existingMap = await db.PrdbWantedVideos
            .Where(w => incomingIds.Contains(w.VideoId))
            .ToDictionaryAsync(w => w.VideoId, ct);

        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            if (existingMap.TryGetValue(item.VideoId, out var existing))
            {
                // Only update fulfillment fields from the API if not already fulfilled
                // locally. Local fulfillment (set by DownloadPollService) takes precedence.
                if (!existing.IsFulfilled)
                {
                    existing.IsFulfilled           = item.IsFulfilled;
                    existing.FulfilledAtUtc        = item.FulfilledAtUtc;
                    existing.FulfilledInQuality    = item.FulfilledInQuality;
                    existing.FulfillmentExternalId = item.FulfillmentExternalId;
                    existing.FulfillmentByApp      = item.FulfillmentByApp;
                }
                existing.PrdbUpdatedAtUtc = item.UpdatedAtUtc;
                existing.SyncedAtUtc      = now;
            }
            else
            {
                db.PrdbWantedVideos.Add(new PrdbWantedVideo
                {
                    VideoId                = item.VideoId,
                    IsFulfilled            = item.IsFulfilled,
                    FulfilledAtUtc         = item.FulfilledAtUtc,
                    FulfilledInQuality     = item.FulfilledInQuality,
                    FulfillmentExternalId  = item.FulfillmentExternalId,
                    FulfillmentByApp       = item.FulfillmentByApp,
                    PrdbCreatedAtUtc       = item.CreatedAtUtc,
                    PrdbUpdatedAtUtc       = item.UpdatedAtUtc,
                    SyncedAtUtc            = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return items.Count;
    }

    // ── Phase 4: Delete removed entries ──────────────────────────────────────

    private async Task<int> DeleteRemovedEntriesAsync(HashSet<Guid> currentIds, CancellationToken ct)
    {
        var toDelete = await db.PrdbWantedVideos
            .Where(w => !currentIds.Contains(w.VideoId))
            .ToListAsync(ct);

        if (toDelete.Count == 0) return 0;

        db.PrdbWantedVideos.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);
        return toDelete.Count;
    }

    // ── HTTP client ──────────────────────────────────────────────────────────

    private HttpClient CreateClient(AppSettings settings)
    {
        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);
        return http;
    }
}
