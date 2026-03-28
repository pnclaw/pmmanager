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
public class PrdbStatusController(AppDbContext db, IHttpClientFactory httpClientFactory, PrdbActorSyncService actorSyncService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet]
    [EndpointSummary("Get prdb status")]
    [EndpointDescription("Returns actor backfill progress and rate limit info from the prdb API.")]
    [ProducesResponseType(typeof(PrdbStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings   = await db.AppSettings.FirstAsync(ct);
        var actorCount = await db.PrdbActors.CountAsync(ct);

        var backfill = new ActorBackfillStatus
        {
            IsComplete   = settings.PrdbActorSyncPage is null,
            CurrentPage  = settings.PrdbActorSyncPage,
            TotalActors  = settings.PrdbActorTotalCount,
            ActorsInDb   = actorCount,
            LastSyncedAt = settings.PrdbActorLastSyncedAt,
        };

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

        return Ok(new PrdbStatusResponse { ActorBackfill = backfill, RateLimit = rateLimit });
    }

    [HttpPost("backfill/run")]
    [EndpointSummary("Run actor backfill")]
    [EndpointDescription("Manually triggers one backfill run, identical to the scheduled SyncWorker tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunBackfill(CancellationToken ct)
    {
        await actorSyncService.RunAsync(ct);
        return NoContent();
    }
}

public class PrdbStatusResponse
{
    public ActorBackfillStatus ActorBackfill { get; init; } = null!;
    public PrdbRateLimitStatus? RateLimit { get; init; }
}

public class ActorBackfillStatus
{
    public bool IsComplete { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalActors { get; init; }
    public int ActorsInDb { get; init; }
    public DateTime? LastSyncedAt { get; init; }
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
