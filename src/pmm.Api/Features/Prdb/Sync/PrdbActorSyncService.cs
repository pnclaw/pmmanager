using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb.Sync;

public class PrdbActorSyncService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<PrdbActorSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int PageSize = 500;

    public async Task RunAsync(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("PrdbActorSyncService: PrdbApiKey not configured, skipping");
            return;
        }

        var http = CreateClient(settings);

        if (settings.PrdbActorSyncPage is not null)
            await RunBackfillPageAsync(http, settings, ct);
        else if (settings.PrdbActorLastSyncedAt is not null)
            await RunNewActorCheckAsync(http, settings, ct);
    }

    // ── Backfill (one page per run) ──────────────────────────────────────────

    private async Task RunBackfillPageAsync(HttpClient http, AppSettings settings, CancellationToken ct)
    {
        var page = settings.PrdbActorSyncPage!.Value;
        logger.LogInformation("PrdbActorSyncService: backfill page {Page}", page);

        var url = $"actors?Page={page}&PageSize={PageSize}&SortBy=createdAtUtc&SortDirection=asc";
        var response = await http.GetFromJsonAsync<PrdbApiPagedResult<PrdbApiActorSummary>>(url, JsonOptions, ct);

        if (response is null || response.Items.Count == 0)
        {
            settings.PrdbActorSyncPage    = null;
            settings.PrdbActorLastSyncedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PrdbActorSyncService: backfill complete");
            return;
        }

        var inserted = await UpsertNewActorsAsync(response.Items, ct);

        var fetched = (long)page * PageSize;
        var done    = fetched >= response.TotalCount;

        settings.PrdbActorTotalCount   = response.TotalCount;
        settings.PrdbActorSyncPage     = done ? null : page + 1;
        settings.PrdbActorLastSyncedAt = done ? DateTime.UtcNow : settings.PrdbActorLastSyncedAt;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PrdbActorSyncService: backfill page {Page} — inserted {Inserted}, total known {Total}, next: {Next}",
            page, inserted, response.TotalCount,
            settings.PrdbActorSyncPage?.ToString() ?? "done");
    }

    // ── New-actor check (runs every tick after backfill) ─────────────────────

    private async Task RunNewActorCheckAsync(HttpClient http, AppSettings settings, CancellationToken ct)
    {
        var since = settings.PrdbActorLastSyncedAt!.Value;
        logger.LogInformation("PrdbActorSyncService: checking for new actors since {Since:O}", since);

        var sinceEncoded = Uri.EscapeDataString(since.ToString("O"));
        var allActors    = new List<PrdbApiActorSummary>();
        var page         = 1;

        while (true)
        {
            var url      = $"actors?CreatedAfter={sinceEncoded}&SortBy=createdAtUtc&SortDirection=asc&Page={page}&PageSize={PageSize}";
            var response = await http.GetFromJsonAsync<PrdbApiPagedResult<PrdbApiActorSummary>>(url, JsonOptions, ct);

            if (response is null || response.Items.Count == 0) break;

            allActors.AddRange(response.Items);

            if (allActors.Count >= response.TotalCount) break;

            page++;
        }

        var inserted = allActors.Count > 0 ? await UpsertNewActorsAsync(allActors, ct) : 0;

        settings.PrdbActorLastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PrdbActorSyncService: new-actor check complete — {Found} found, {Inserted} inserted",
            allActors.Count, inserted);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private async Task<int> UpsertNewActorsAsync(List<PrdbApiActorSummary> actors, CancellationToken ct)
    {
        var incomingIds = actors.Select(a => a.Id).ToList();
        var existingIds = await db.PrdbActors
            .Where(a => incomingIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToHashSetAsync(ct);

        var now      = DateTime.UtcNow;
        var toInsert = actors
            .Where(a => !existingIds.Contains(a.Id))
            .Select(a => new PrdbActor
            {
                Id               = a.Id,
                Name             = a.Name,
                Gender           = a.Gender,
                Birthday         = a.Birthday,
                Nationality      = a.Nationality,
                Ethnicity        = a.Ethnicity,
                PrdbCreatedAtUtc = now,
                PrdbUpdatedAtUtc = now,
                SyncedAtUtc      = now,
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            db.PrdbActors.AddRange(toInsert);
            await db.SaveChangesAsync(ct);
        }

        return toInsert.Count;
    }

    private HttpClient CreateClient(AppSettings settings)
    {
        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);
        return http;
    }
}
