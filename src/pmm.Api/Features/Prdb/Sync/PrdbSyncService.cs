using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb.Sync;

public class PrdbSyncService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<PrdbSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int PageSize = 500;
    private const int LatestVideosLimit = 1500;

    public async Task<PrdbSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            logger.LogWarning("PrdbSyncService: PrdbApiKey is not configured — skipping sync");
            return new PrdbSyncResult();
        }

        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);

        var networksUpserted    = await SyncSitesAsync(http, ct);
        var sitesUpserted       = networksUpserted.sitesUpserted;
        var favSitesUpserted    = await SyncFavoriteSitesAsync(http, ct);
        var favActorsUpserted   = await SyncFavoriteActorsAsync(http, ct);
        var videosUpserted      = await SyncVideosAsync(http, ct);

        return new PrdbSyncResult
        {
            NetworksUpserted    = networksUpserted.networksUpserted,
            SitesUpserted       = sitesUpserted,
            FavoriteSitesSynced = favSitesUpserted,
            FavoriteActorsSynced = favActorsUpserted,
            VideosUpserted      = videosUpserted,
        };
    }

    // ── Sites + Networks ─────────────────────────────────────────────────────

    private async Task<(int networksUpserted, int sitesUpserted)> SyncSitesAsync(HttpClient http, CancellationToken ct)
    {
        logger.LogInformation("PrdbSyncService: syncing sites");

        var apiSites = await FetchAllPagesAsync<PrdbApiSite>(http, "sites", ct);

        // Upsert networks derived from site data
        var networksFromApi = apiSites
            .Where(s => s.NetworkId.HasValue && s.NetworkTitle != null)
            .GroupBy(s => s.NetworkId!.Value)
            .Select(g => g.First())
            .ToList();

        var existingNetworks = await db.PrdbNetworks
            .ToDictionaryAsync(n => n.Id, ct);

        var now = DateTime.UtcNow;

        foreach (var apiSite in networksFromApi)
        {
            if (existingNetworks.TryGetValue(apiSite.NetworkId!.Value, out var existing))
            {
                existing.Title       = apiSite.NetworkTitle!;
                existing.SyncedAtUtc = now;
            }
            else
            {
                var network = new PrdbNetwork
                {
                    Id          = apiSite.NetworkId!.Value,
                    Title       = apiSite.NetworkTitle!,
                    Url         = string.Empty,
                    SyncedAtUtc = now,
                };
                db.PrdbNetworks.Add(network);
                existingNetworks[network.Id] = network;
            }
        }

        await db.SaveChangesAsync(ct);
        var networksUpserted = networksFromApi.Count;

        // Upsert sites
        var existingSites = await db.PrdbSites
            .ToDictionaryAsync(s => s.Id, ct);

        foreach (var apiSite in apiSites)
        {
            if (existingSites.TryGetValue(apiSite.Id, out var existing))
            {
                existing.Title       = apiSite.Title;
                existing.Url         = apiSite.Url;
                existing.NetworkId   = apiSite.NetworkId;
                existing.SyncedAtUtc = now;
            }
            else
            {
                db.PrdbSites.Add(new PrdbSite
                {
                    Id          = apiSite.Id,
                    Title       = apiSite.Title,
                    Url         = apiSite.Url,
                    NetworkId   = apiSite.NetworkId,
                    SyncedAtUtc = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("PrdbSyncService: upserted {Networks} networks, {Sites} sites",
            networksUpserted, apiSites.Count);

        return (networksUpserted, apiSites.Count);
    }

    // ── Favorite Sites ───────────────────────────────────────────────────────

    private async Task<int> SyncFavoriteSitesAsync(HttpClient http, CancellationToken ct)
    {
        logger.LogInformation("PrdbSyncService: syncing favorite sites");

        var apiFavorites = await FetchAllPagesAsync<PrdbApiFavoriteSite>(http, "favorite-sites", ct);
        var favoriteMap  = apiFavorites.ToDictionary(f => f.Id);

        var allSites = await db.PrdbSites.ToListAsync(ct);

        foreach (var site in allSites)
        {
            if (favoriteMap.TryGetValue(site.Id, out var fav))
            {
                site.IsFavorite      = true;
                site.FavoritedAtUtc  = fav.FavoritedAtUtc;
            }
            else if (site.IsFavorite)
            {
                site.IsFavorite     = false;
                site.FavoritedAtUtc = null;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("PrdbSyncService: {Count} favorite sites synced", apiFavorites.Count);
        return apiFavorites.Count;
    }

    // ── Favorite Actors ──────────────────────────────────────────────────────

    private async Task<int> SyncFavoriteActorsAsync(HttpClient http, CancellationToken ct)
    {
        logger.LogInformation("PrdbSyncService: syncing favorite actors");

        var apiFavorites = await FetchAllPagesAsync<PrdbApiFavoriteActor>(http, "favorite-actors", ct);
        var favoriteMap  = apiFavorites.ToDictionary(f => f.Id);

        var existingActors = await db.PrdbActors
            .Include(a => a.Aliases)
            .ToDictionaryAsync(a => a.Id, ct);

        var now = DateTime.UtcNow;

        // Insert favorite actors not yet in the DB
        foreach (var fav in apiFavorites.Where(f => !existingActors.ContainsKey(f.Id)))
        {
            var detail = await http.GetFromJsonAsync<PrdbApiActorDetail>(
                $"actors/{fav.Id}", JsonOptions, ct);

            if (detail is null) continue;

            var actor = new PrdbActor
            {
                Id               = detail.Id,
                Name             = detail.Name,
                Gender           = detail.Gender,
                Birthday         = detail.Birthday,
                BirthdayType     = detail.BirthdayType,
                Deathday         = detail.Deathday,
                Birthplace       = detail.Birthplace,
                Haircolor        = detail.Haircolor,
                Eyecolor         = detail.Eyecolor,
                BreastType       = detail.BreastType,
                Height           = detail.Height,
                BraSize          = detail.BraSize,
                BraSizeLabel     = detail.BraSizeLabel,
                WaistSize        = detail.WaistSize,
                HipSize          = detail.HipSize,
                Nationality      = detail.Nationality,
                Ethnicity        = detail.Ethnicity,
                CareerStart      = detail.CareerStart,
                CareerEnd        = detail.CareerEnd,
                Tattoos          = detail.Tattoos,
                Piercings        = detail.Piercings,
                IsFavorite       = true,
                FavoritedAtUtc   = fav.FavoritedAtUtc,
                PrdbCreatedAtUtc = detail.CreatedAtUtc,
                PrdbUpdatedAtUtc = detail.UpdatedAtUtc,
                SyncedAtUtc      = now,
                Aliases          = detail.Aliases
                    .Select(a => new PrdbActorAlias { Name = a.Name, SiteId = a.SiteId })
                    .ToList(),
            };

            db.PrdbActors.Add(actor);
            existingActors[actor.Id] = actor;
        }

        await db.SaveChangesAsync(ct);

        // Update IsFavorite flag on all existing actors
        foreach (var actor in existingActors.Values)
        {
            if (favoriteMap.TryGetValue(actor.Id, out var fav))
            {
                actor.IsFavorite     = true;
                actor.FavoritedAtUtc = fav.FavoritedAtUtc;
            }
            else if (actor.IsFavorite)
            {
                actor.IsFavorite     = false;
                actor.FavoritedAtUtc = null;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("PrdbSyncService: {Count} favorite actors synced", apiFavorites.Count);
        return apiFavorites.Count;
    }

    // ── Videos ───────────────────────────────────────────────────────────────

    private async Task<int> SyncVideosAsync(HttpClient http, CancellationToken ct)
    {
        logger.LogInformation("PrdbSyncService: syncing videos");

        var favoriteSiteIds = await db.PrdbSites
            .Where(s => s.IsFavorite)
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Collect all videos to upsert, deduplicated by ID
        var allApiVideos = new Dictionary<Guid, PrdbApiVideo>();

        // Latest 1500 global videos
        var latestVideos = await FetchAllPagesAsync<PrdbApiVideo>(http, "videos", ct, maxItems: LatestVideosLimit);
        foreach (var v in latestVideos)
            allApiVideos[v.Id] = v;

        // All videos for each favorite site
        foreach (var siteId in favoriteSiteIds)
        {
            var siteVideos = await FetchAllPagesAsync<PrdbApiVideo>(http, $"videos?SiteId={siteId}", ct);
            foreach (var v in siteVideos)
                allApiVideos[v.Id] = v;
        }

        // All videos for each favorite actor
        var favoriteActorIds = await db.PrdbActors
            .Where(a => a.IsFavorite)
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var actorId in favoriteActorIds)
        {
            var actorVideos = await FetchAllPagesAsync<PrdbApiVideo>(http, $"videos?ActorId={actorId}", ct);
            foreach (var v in actorVideos)
                allApiVideos[v.Id] = v;
        }

        if (allApiVideos.Count == 0)
        {
            logger.LogInformation("PrdbSyncService: no videos to sync");
            return 0;
        }

        // Load existing video IDs to determine inserts vs updates
        var existingIds = await db.PrdbVideos
            .Where(v => allApiVideos.Keys.Contains(v.Id))
            .Select(v => v.Id)
            .ToHashSetAsync(ct);

        // Load known site IDs so we can skip videos whose site isn't synced
        var knownSiteIds = await db.PrdbSites
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;

        var toInsert = allApiVideos.Values
            .Where(v => !existingIds.Contains(v.Id) && knownSiteIds.Contains(v.SiteId))
            .Select(v => new PrdbVideo
            {
                Id               = v.Id,
                Title            = v.Title,
                ReleaseDate      = v.ReleaseDate,
                SiteId           = v.SiteId,
                PrdbCreatedAtUtc = now,
                PrdbUpdatedAtUtc = now,
                SyncedAtUtc      = now,
            })
            .ToList();

        var toUpdate = allApiVideos.Values
            .Where(v => existingIds.Contains(v.Id))
            .ToList();

        db.PrdbVideos.AddRange(toInsert);
        await db.SaveChangesAsync(ct);

        foreach (var batch in toUpdate.Chunk(200))
        {
            var ids = batch.Select(v => v.Id).ToList();
            var entities = await db.PrdbVideos
                .Where(v => ids.Contains(v.Id))
                .ToListAsync(ct);

            var lookup = batch.ToDictionary(v => v.Id);
            foreach (var entity in entities)
            {
                var api = lookup[entity.Id];
                entity.Title            = api.Title;
                entity.ReleaseDate      = api.ReleaseDate;
                entity.PrdbUpdatedAtUtc = now;
                entity.SyncedAtUtc      = now;
            }

            await db.SaveChangesAsync(ct);
        }

        var total = toInsert.Count + toUpdate.Count;
        logger.LogInformation("PrdbSyncService: upserted {Videos} videos ({Inserted} new, {Updated} updated)",
            total, toInsert.Count, toUpdate.Count);

        return total;
    }

    // ── Pagination helper ────────────────────────────────────────────────────

    private async Task<List<T>> FetchAllPagesAsync<T>(
        HttpClient http,
        string endpoint,
        CancellationToken ct,
        int? maxItems = null)
    {
        var results = new List<T>();
        var page = 1;
        var separator = endpoint.Contains('?') ? '&' : '?';

        while (true)
        {
            var url = $"{endpoint}{separator}Page={page}&PageSize={PageSize}";
            var response = await http.GetFromJsonAsync<PrdbApiPagedResult<T>>(url, JsonOptions, ct);

            if (response is null || response.Items.Count == 0) break;

            results.AddRange(response.Items);

            if (maxItems.HasValue && results.Count >= maxItems.Value)
            {
                results = results.Take(maxItems.Value).ToList();
                break;
            }

            if (results.Count >= response.TotalCount) break;

            page++;
        }

        return results;
    }
}
