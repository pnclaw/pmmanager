using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;
using pmm.Api.Features.Indexers.Matching;
using pmm.Api.Features.Indexers.Scraping;
using pmm.Api.Features.Prdb.Sync;

namespace pmm.Api.Features.Prdb;

[ApiController]
[Route("api/prdb-status")]
[Produces("application/json")]
public class PrdbStatusController(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    PrdbActorSyncService actorSyncService,
    PrdbVideoDetailSyncService videoDetailSyncService,
    PrdbLatestPreDbSyncService latestPreDbSyncService,
    PrdbWantedVideoSyncService wantedVideoSyncService,
    IndexerBackfillService indexerBackfillService,
    IndexerRowMatchService indexerRowMatchService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet]
    [EndpointSummary("Get prdb status")]
    [EndpointDescription("Returns actor backfill progress, detail sync progress, library counts, and rate limit info.")]
    [ProducesResponseType(typeof(PrdbStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);

        // ── SyncWorker schedule ───────────────────────────────────────────────
        var syncWorker = new SyncWorkerStatus
        {
            IntervalMinutes = 15,
            LastRunAt       = settings.SyncWorkerLastRunAt,
            NextRunAt       = settings.SyncWorkerLastRunAt?.AddMinutes(15),
        };

        // ── Actor summary backfill ────────────────────────────────────────────
        var actorCount = await db.PrdbActors.CountAsync(ct);

        var actorBackfill = new ActorBackfillStatus
        {
            IsComplete   = settings.PrdbActorSyncPage is null,
            CurrentPage  = settings.PrdbActorSyncPage,
            TotalActors  = settings.PrdbActorTotalCount,
            ActorsInDb   = actorCount,
            LastSyncedAt = settings.PrdbActorLastSyncedAt,
        };

        // ── Actor detail sync ─────────────────────────────────────────────────
        var actorsWithDetail  = await db.PrdbActors.CountAsync(a => a.DetailSyncedAtUtc != null, ct);
        var favoriteActors    = await db.PrdbActors.CountAsync(a => a.IsFavorite, ct);

        var actorDetailSync = new ActorDetailSyncStatus
        {
            ActorsWithDetail  = actorsWithDetail,
            ActorsPending     = actorCount - actorsWithDetail,
            TotalActors       = actorCount,
            FavoriteActors    = favoriteActors,
        };

        // ── Video detail sync ─────────────────────────────────────────────────
        var videoCount       = await db.PrdbVideos.CountAsync(ct);
        var videosWithDetail = await db.PrdbVideos.CountAsync(v => v.DetailSyncedAtUtc != null, ct);
        var videosWithCast   = await db.PrdbVideoActors.Select(va => va.VideoId).Distinct().CountAsync(ct);

        var videoDetailSync = new VideoDetailSyncStatus
        {
            VideosWithDetail = videosWithDetail,
            VideosPending    = videoCount - videosWithDetail,
            TotalVideos      = videoCount,
            VideosWithCast   = videosWithCast,
        };

        // ── Wanted video sync ─────────────────────────────────────────────────
        var totalWanted     = await db.PrdbWantedVideos.CountAsync(ct);
        var fulfilled       = await db.PrdbWantedVideos.CountAsync(w => w.IsFulfilled, ct);
        var pendingDetail   = await db.PrdbWantedVideos
            .Join(db.PrdbVideos, w => w.VideoId, v => v.Id, (w, v) => v.DetailSyncedAtUtc)
            .CountAsync(d => d == null, ct);

        var wantedVideoSync = new WantedVideoSyncStatus
        {
            Total         = totalWanted,
            Unfulfilled   = totalWanted - fulfilled,
            Fulfilled     = fulfilled,
            PendingDetail = pendingDetail,
            LastSyncedAt  = settings.PrdbWantedVideoLastSyncedAt,
        };

        // ── Prename sync ──────────────────────────────────────────────────────
        var totalPreDbEntries = await db.PrdbPreDbEntries.CountAsync(ct);
        var totalLinkedPreDbEntries = await db.PrdbPreDbEntries.CountAsync(p => p.PrdbVideoId != null, ct);

        var preNameSync = new PreNameSyncStatus
        {
            TotalPreNames       = totalLinkedPreDbEntries,
            TotalPreDbEntries   = totalPreDbEntries,
            IsBackfilling       = settings.PrenamesBackfillPage is not null || settings.PrenamesSyncCursorUtc is null,
            BackfillPage        = settings.PrenamesBackfillPage,
            BackfillTotalCount  = settings.PrenamesBackfillTotalCount,
            LastSyncedAt        = settings.PrenamesSyncCursorUtc,
        };

        // ── Library counts ────────────────────────────────────────────────────
        var library = new LibraryCounts
        {
            Networks       = await db.PrdbNetworks.CountAsync(ct),
            Sites          = await db.PrdbSites.CountAsync(ct),
            FavoriteSites  = await db.PrdbSites.CountAsync(s => s.IsFavorite, ct),
            Videos         = videoCount,
            PreDbEntries   = totalPreDbEntries,
            PreNames       = totalLinkedPreDbEntries,
            Actors         = actorCount,
            FavoriteActors = favoriteActors,
            ActorImages    = await db.PrdbActorImages.CountAsync(ct),
            VideoImages    = await db.PrdbVideoImages.CountAsync(ct),
            WantedVideos   = totalWanted,
        };

        // ── Indexer row match sync ────────────────────────────────────────────
        var totalMatches = await db.IndexerRowMatches.CountAsync(ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var topGrouped = await db.IndexerRows
            .GroupBy(r => r.IndexerId)
            .Select(g => new
            {
                IndexerId    = g.Key,
                TotalRows    = g.Count(),
                RowsLastWeek = g.Count(r => r.CreatedAt >= weekAgo),
            })
            .OrderByDescending(g => g.TotalRows)
            .Take(3)
            .ToListAsync(ct);

        var topIndexerIds = topGrouped.Select(g => g.IndexerId).ToList();
        var indexerTitles = await db.Indexers
            .Where(i => topIndexerIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Title })
            .ToDictionaryAsync(i => i.Id, i => i.Title, ct);
        var currentIndexerTitle = settings.IndexerBackfillCurrentIndexerId is null
            ? null
            : await db.Indexers
                .Where(i => i.Id == settings.IndexerBackfillCurrentIndexerId.Value)
                .Select(i => i.Title)
                .FirstOrDefaultAsync(ct);

        var topIndexers = topGrouped
            .Select(g => new IndexerRowStat
            {
                Title        = indexerTitles.GetValueOrDefault(g.IndexerId, "Unknown"),
                TotalRows    = g.TotalRows,
                RowsLastWeek = g.RowsLastWeek,
            })
            .ToList();

        var indexerBackfill = new IndexerBackfillStatus
        {
            Days = settings.IndexerBackfillDays,
            StartedAtUtc = settings.IndexerBackfillStartedAtUtc,
            CutoffUtc = settings.IndexerBackfillCutoffUtc,
            CompletedAtUtc = settings.IndexerBackfillCompletedAtUtc,
            LastRunAtUtc = settings.IndexerBackfillLastRunAtUtc,
            CurrentIndexerId = settings.IndexerBackfillCurrentIndexerId,
            CurrentIndexerTitle = currentIndexerTitle,
            CurrentOffset = settings.IndexerBackfillCurrentOffset,
            IsComplete = settings.IndexerBackfillCompletedAtUtc != null,
        };

        var indexerRowMatchSync = new IndexerRowMatchSyncStatus
        {
            TotalMatches = totalMatches,
            LastRunAt    = settings.IndexerRowMatchLastRunAt,
            TopIndexers  = topIndexers,
        };

        // ── Rate limits ───────────────────────────────────────────────────────
        PrdbRateLimitStatus? rateLimit = null;
        if (!string.IsNullOrWhiteSpace(settings.PrdbApiKey))
        {
            try
            {
                var http = httpClientFactory.CreateClient();
                http.BaseAddress = new Uri(settings.PrdbApiUrl.TrimEnd('/') + "/");
                http.DefaultRequestHeaders.Add("X-Api-Key", settings.PrdbApiKey);
                rateLimit = await http.GetFromJsonAsync<PrdbRateLimitStatus>("rate-limit", JsonOptions, ct);
            }
            catch { /* rate limit unavailable — return null */ }
        }

        return Ok(new PrdbStatusResponse
        {
            SyncWorker           = syncWorker,
            ActorBackfill        = actorBackfill,
            ActorDetailSync      = actorDetailSync,
            VideoDetailSync      = videoDetailSync,
            PreNameSync          = preNameSync,
            WantedVideoSync      = wantedVideoSync,
            IndexerBackfill      = indexerBackfill,
            IndexerRowMatchSync  = indexerRowMatchSync,
            Library              = library,
            RateLimit            = rateLimit,
        });
    }

    [HttpPost("backfill/run")]
    [EndpointSummary("Run actor backfill")]
    [EndpointDescription("Manually triggers one actor summary backfill run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunBackfill(CancellationToken ct)
    {
        await actorSyncService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("video-detail-sync/run")]
    [EndpointSummary("Run video detail sync")]
    [EndpointDescription("Manually triggers one video detail sync run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunVideoDetailSync(CancellationToken ct)
    {
        await videoDetailSyncService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("prename-sync/run")]
    [EndpointSummary("Run PreDb sync")]
    [EndpointDescription("Manually triggers one PreDb sync run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunPreNameSync(CancellationToken ct)
    {
        await latestPreDbSyncService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("prename-sync/reset-cursor")]
    [EndpointSummary("Reset PreDb sync cursor")]
    [EndpointDescription("Clears the PreDb sync cursor so the next run performs a full backfill from the beginning.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPreNameCursor(CancellationToken ct)
    {
        var settings = await db.AppSettings.FirstAsync(ct);
        settings.PrenamesSyncCursorUtc = null;
        settings.PrenamesBackfillPage  = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("wanted-video-sync/run")]
    [EndpointSummary("Run wanted video sync")]
    [EndpointDescription("Manually triggers one wanted video sync run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunWantedVideoSync(CancellationToken ct)
    {
        await wantedVideoSyncService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("indexer-backfill/run")]
    [EndpointSummary("Run indexer backfill")]
    [EndpointDescription("Manually triggers one indexer backfill step, identical to the scheduled 15-minute background run.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunIndexerBackfill(CancellationToken ct)
    {
        await indexerBackfillService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("indexer-row-match/run")]
    [EndpointSummary("Run indexer row match sync")]
    [EndpointDescription("Manually triggers one indexer row match run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunIndexerRowMatch(CancellationToken ct)
    {
        await indexerRowMatchService.RunAsync(ct);
        return NoContent();
    }

    [HttpPost("indexer-row-match/debug")]
    [EndpointSummary("Debug indexer row match")]
    [EndpointDescription("Read-only diagnostic run filtered by a search string. Returns match status for every matching row without writing to the database.")]
    [ProducesResponseType(typeof(IndexerRowMatchDebugResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DebugIndexerRowMatch(
        [FromBody] IndexerRowMatchDebugRequest request, CancellationToken ct)
    {
        var result = await indexerRowMatchService.RunDebugAsync(request.Search, ct);
        return Ok(result);
    }
}

public class IndexerRowMatchDebugRequest
{
    public string Search { get; init; } = string.Empty;
}

public class PrdbStatusResponse
{
    public SyncWorkerStatus SyncWorker { get; init; } = null!;
    public ActorBackfillStatus ActorBackfill { get; init; } = null!;
    public ActorDetailSyncStatus ActorDetailSync { get; init; } = null!;
    public VideoDetailSyncStatus VideoDetailSync { get; init; } = null!;
    public PreNameSyncStatus PreNameSync { get; init; } = null!;
    public WantedVideoSyncStatus WantedVideoSync { get; init; } = null!;
    public IndexerBackfillStatus IndexerBackfill { get; init; } = null!;
    public IndexerRowMatchSyncStatus IndexerRowMatchSync { get; init; } = null!;
    public LibraryCounts Library { get; init; } = null!;
    public PrdbRateLimitStatus? RateLimit { get; init; }
}

public class SyncWorkerStatus
{
    public int IntervalMinutes { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime? NextRunAt { get; init; }
}

public class ActorBackfillStatus
{
    public bool IsComplete { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalActors { get; init; }
    public int ActorsInDb { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}

public class ActorDetailSyncStatus
{
    public int ActorsWithDetail { get; init; }
    public int ActorsPending { get; init; }
    public int TotalActors { get; init; }
    public int FavoriteActors { get; init; }
}

public class VideoDetailSyncStatus
{
    public int VideosWithDetail { get; init; }
    public int VideosPending { get; init; }
    public int TotalVideos { get; init; }
    public int VideosWithCast { get; init; }
}

public class WantedVideoSyncStatus
{
    public int Total { get; init; }
    public int Unfulfilled { get; init; }
    public int Fulfilled { get; init; }
    public int PendingDetail { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}

public class IndexerBackfillStatus
{
    public int Days { get; init; }
    public bool IsComplete { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CutoffUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? LastRunAtUtc { get; init; }
    public Guid? CurrentIndexerId { get; init; }
    public string? CurrentIndexerTitle { get; init; }
    public int? CurrentOffset { get; init; }
}

public class IndexerRowMatchSyncStatus
{
    public int TotalMatches { get; init; }
    public DateTime? LastRunAt { get; init; }
    public List<IndexerRowStat> TopIndexers { get; init; } = [];
}

public class IndexerRowStat
{
    public string Title { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int RowsLastWeek { get; init; }
}

public class PreNameSyncStatus
{
    public int TotalPreNames { get; init; }
    public int TotalPreDbEntries { get; init; }
    public bool IsBackfilling { get; init; }
    public int? BackfillPage { get; init; }
    public int? BackfillTotalCount { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}

public class LibraryCounts
{
    public int Networks { get; init; }
    public int Sites { get; init; }
    public int FavoriteSites { get; init; }
    public int Videos { get; init; }
    public int PreDbEntries { get; init; }
    public int PreNames { get; init; }
    public int Actors { get; init; }
    public int FavoriteActors { get; init; }
    public int ActorImages { get; init; }
    public int VideoImages { get; init; }
    public int WantedVideos { get; init; }
}

public class PrdbRateLimitStatus
{
    public bool IsEnforced { get; init; }
    public RateLimitWindow Hourly { get; init; } = null!;
    public RateLimitWindow Monthly { get; init; } = null!;
}

public class RateLimitWindow
{
    public int Limit { get; init; }
    public int Used { get; init; }
    public int Remaining { get; init; }
    public int ResetsInSeconds { get; init; }
}
