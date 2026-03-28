using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;
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
    PrdbWantedVideoSyncService wantedVideoSyncService) : ControllerBase
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

        // ── Library counts ────────────────────────────────────────────────────
        var library = new LibraryCounts
        {
            Networks       = await db.PrdbNetworks.CountAsync(ct),
            Sites          = await db.PrdbSites.CountAsync(ct),
            FavoriteSites  = await db.PrdbSites.CountAsync(s => s.IsFavorite, ct),
            Videos         = videoCount,
            Actors         = actorCount,
            FavoriteActors = favoriteActors,
            ActorImages    = await db.PrdbActorImages.CountAsync(ct),
            VideoImages    = await db.PrdbVideoImages.CountAsync(ct),
            WantedVideos   = totalWanted,
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
            SyncWorker      = syncWorker,
            ActorBackfill   = actorBackfill,
            ActorDetailSync = actorDetailSync,
            VideoDetailSync = videoDetailSync,
            WantedVideoSync = wantedVideoSync,
            Library         = library,
            RateLimit       = rateLimit,
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

    [HttpPost("wanted-video-sync/run")]
    [EndpointSummary("Run wanted video sync")]
    [EndpointDescription("Manually triggers one wanted video sync run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunWantedVideoSync(CancellationToken ct)
    {
        await wantedVideoSyncService.RunAsync(ct);
        return NoContent();
    }
}

public class PrdbStatusResponse
{
    public SyncWorkerStatus SyncWorker { get; init; } = null!;
    public ActorBackfillStatus ActorBackfill { get; init; } = null!;
    public ActorDetailSyncStatus ActorDetailSync { get; init; } = null!;
    public VideoDetailSyncStatus VideoDetailSync { get; init; } = null!;
    public WantedVideoSyncStatus WantedVideoSync { get; init; } = null!;
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

public class LibraryCounts
{
    public int Networks { get; init; }
    public int Sites { get; init; }
    public int FavoriteSites { get; init; }
    public int Videos { get; init; }
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
