using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb.Sync;

public class PrdbLatestPreNameSyncService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<PrdbLatestPreNameSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int PageSize    = 500;
    private const int PagesPerRun = 10; // 5,000 prenames per run, 10 API requests

    public async Task RunAsync(CancellationToken ct = default)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("PrdbLatestPreNameSyncService: PrdbApiKey not configured — skipping");
            return;
        }

        var http = CreateClient(settings);

        if (settings.PrenamesBackfillPage is not null || settings.PrenamesSyncCursorUtc is null)
            await RunBackfillAsync(http, settings, ct);
        else
            await RunIncrementalAsync(http, settings, ct);
    }

    // ── Backfill (up to PagesPerRun pages per run) ───────────────────────────

    private async Task RunBackfillAsync(HttpClient http, AppSettings settings, CancellationToken ct)
    {
        var startPage     = settings.PrenamesBackfillPage ?? 1;
        var currentPage   = startPage;
        var totalInserted = 0;
        var done          = false;

        logger.LogInformation("PrdbLatestPreNameSyncService: backfill starting at page {Page}", startPage);

        for (var i = 0; i < PagesPerRun; i++)
        {
            var url      = $"prenames/latest?Page={currentPage}&PageSize={PageSize}";
            var response = await http.GetFromJsonAsync<PrdbApiPagedResult<PrdbApiLatestPreNameItem>>(
                url, JsonOptions, ct);

            if (response is null || response.Items.Count == 0)
            {
                done = true;
                break;
            }

            totalInserted += await UpsertPreNamesAsync(response.Items, ct);
            settings.PrenamesBackfillTotalCount = response.TotalCount;

            var fetched = (long)currentPage * PageSize;
            currentPage++;

            if (fetched >= response.TotalCount)
            {
                done = true;
                break;
            }
        }

        settings.PrenamesBackfillPage  = done ? null : currentPage;
        settings.PrenamesSyncCursorUtc = done ? DateTime.UtcNow : settings.PrenamesSyncCursorUtc;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PrdbLatestPreNameSyncService: backfill pages {Start}–{End} — inserted {Inserted}, next: {Next}",
            startPage, currentPage - 1, totalInserted,
            settings.PrenamesBackfillPage?.ToString() ?? "done");
    }

    // ── Incremental sync (runs every tick after backfill is complete) ─────────

    private async Task RunIncrementalAsync(HttpClient http, AppSettings settings, CancellationToken ct)
    {
        var cursor       = settings.PrenamesSyncCursorUtc!.Value;
        var runStartedAt = DateTime.UtcNow;

        logger.LogInformation("PrdbLatestPreNameSyncService: incremental sync since {Cursor:O}", cursor);

        var allItems    = new List<PrdbApiLatestPreNameItem>();
        var page        = 1;
        var cursorParam = Uri.EscapeDataString(cursor.ToString("O"));

        while (true)
        {
            var url      = $"prenames/latest?Page={page}&PageSize={PageSize}&CreatedFrom={cursorParam}";
            var response = await http.GetFromJsonAsync<PrdbApiPagedResult<PrdbApiLatestPreNameItem>>(
                url, JsonOptions, ct);

            if (response is null || response.Items.Count == 0) break;

            allItems.AddRange(response.Items);
            if (allItems.Count >= response.TotalCount) break;
            page++;
        }

        var inserted = allItems.Count > 0 ? await UpsertPreNamesAsync(allItems, ct) : 0;

        settings.PrenamesSyncCursorUtc = runStartedAt;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PrdbLatestPreNameSyncService: incremental sync complete — {Found} found, {Inserted} inserted",
            allItems.Count, inserted);
    }

    // ── Shared upsert ────────────────────────────────────────────────────────

    private async Task<int> UpsertPreNamesAsync(List<PrdbApiLatestPreNameItem> items, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Upsert site stubs for any sites not yet in the DB
        var siteItems = items.Select(i => i.Video.Site).DistinctBy(s => s.Id).ToDictionary(s => s.Id);
        var existingSiteIds = await db.PrdbSites
            .Where(s => siteItems.Keys.Contains(s.Id))
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        foreach (var site in siteItems.Values.Where(s => !existingSiteIds.Contains(s.Id)))
        {
            db.PrdbSites.Add(new PrdbSite
            {
                Id          = site.Id,
                Title       = site.Title,
                Url         = string.Empty,
                SyncedAtUtc = now,
            });
        }
        await db.SaveChangesAsync(ct);

        // Upsert video stubs for any videos not yet in the DB
        var videoItems = items.Select(i => i.Video).DistinctBy(v => v.Id).ToDictionary(v => v.Id);
        var existingVideoIds = await db.PrdbVideos
            .Where(v => videoItems.Keys.Contains(v.Id))
            .Select(v => v.Id)
            .ToHashSetAsync(ct);

        foreach (var video in videoItems.Values.Where(v => !existingVideoIds.Contains(v.Id)))
        {
            db.PrdbVideos.Add(new PrdbVideo
            {
                Id               = video.Id,
                Title            = video.Title,
                ReleaseDate      = video.ReleaseDate,
                SiteId           = video.Site.Id,
                PrdbCreatedAtUtc = now,
                PrdbUpdatedAtUtc = now,
                SyncedAtUtc      = now,
            });
        }
        await db.SaveChangesAsync(ct);

        // Insert new prenames
        var existingPreNameIds = await db.PrdbVideoPreNames
            .Where(p => items.Select(i => i.Id).Contains(p.Id))
            .Select(p => p.Id)
            .ToHashSetAsync(ct);

        var inserted = 0;
        foreach (var item in items.Where(i => !existingPreNameIds.Contains(i.Id)))
        {
            db.PrdbVideoPreNames.Add(new PrdbVideoPreName
            {
                Id      = item.Id,
                Title   = item.Title,
                VideoId = item.Video.Id,
            });
            inserted++;
        }

        if (inserted > 0)
            await db.SaveChangesAsync(ct);

        return inserted;
    }

    private HttpClient CreateClient(AppSettings settings)
    {
        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);
        return http;
    }
}
