using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Settings;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Get application settings")]
    [EndpointDescription("Returns the current application settings.")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var settings = await db.AppSettings.FirstAsync();
        return Ok(ToResponse(settings));
    }

    [HttpPut]
    [Consumes("application/json")]
    [EndpointSummary("Update application settings")]
    [EndpointDescription("Updates the application settings.")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request)
    {
        var settings = await db.AppSettings.FirstAsync();
        var previousIndexerBackfillDays = settings.IndexerBackfillDays;

        settings.PrdbApiKey = request.PrdbApiKey;
        settings.PrdbApiUrl = request.PrdbApiUrl;
        settings.PreferredVideoQuality = request.PreferredVideoQuality;
        settings.SafeForWork = request.SafeForWork;
        settings.IndexerBackfillDays = request.IndexerBackfillDays;

        if (request.IndexerBackfillDays > previousIndexerBackfillDays)
            ResetIndexerBackfillState(settings);

        await db.SaveChangesAsync();
        return Ok(ToResponse(settings));
    }

    [HttpPost("reset-prdb-data")]
    [EndpointSummary("Reset all cached prdb.net data")]
    [EndpointDescription("Deletes all data cached from prdb.net and resets sync cursors. API credentials and unrelated settings are not affected.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPrdbData()
    {
        await db.IndexerRowMatches.ExecuteDeleteAsync();
        await db.DownloadLogs.ExecuteDeleteAsync();
        await db.PrdbWantedVideos.ExecuteDeleteAsync();
        await db.PrdbVideoActors.ExecuteDeleteAsync();
        await db.PrdbVideoImages.ExecuteDeleteAsync();
        await db.PrdbPreDbEntries.ExecuteDeleteAsync();
        await db.PrdbVideos.ExecuteDeleteAsync();
        await db.PrdbSites.ExecuteDeleteAsync();
        await db.PrdbNetworks.ExecuteDeleteAsync();
        await db.PrdbActorImages.ExecuteDeleteAsync();
        await db.PrdbActorAliases.ExecuteDeleteAsync();
        await db.PrdbActors.ExecuteDeleteAsync();

        var settings = await db.AppSettings.FirstAsync();
        settings.PrdbActorSyncPage          = 1;
        settings.PrdbActorLastSyncedAt      = null;
        settings.PrdbActorTotalCount        = null;
        settings.SyncWorkerLastRunAt        = null;
        settings.PrdbWantedVideoLastSyncedAt = null;
        settings.IndexerRowMatchLastRunAt   = null;
        settings.PrenamesBackfillPage       = 1;
        settings.PrenamesBackfillTotalCount = null;
        settings.PrenamesSyncCursorUtc      = null;
        settings.IndexerBackfillStartedAtUtc = null;
        settings.IndexerBackfillCutoffUtc = null;
        settings.IndexerBackfillCompletedAtUtc = null;
        settings.IndexerBackfillLastRunAtUtc = null;
        settings.IndexerBackfillCurrentIndexerId = null;
        settings.IndexerBackfillCurrentOffset = null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static SettingsResponse ToResponse(AppSettings settings) => new()
    {
        PrdbApiKey = settings.PrdbApiKey,
        PrdbApiUrl = settings.PrdbApiUrl,
        PreferredVideoQuality = (int)settings.PreferredVideoQuality,
        SafeForWork = settings.SafeForWork,
        IndexerBackfillDays = settings.IndexerBackfillDays,
    };

    private static void ResetIndexerBackfillState(AppSettings settings)
    {
        settings.IndexerBackfillStartedAtUtc = null;
        settings.IndexerBackfillCutoffUtc = null;
        settings.IndexerBackfillCompletedAtUtc = null;
        settings.IndexerBackfillLastRunAtUtc = null;
        settings.IndexerBackfillCurrentIndexerId = null;
        settings.IndexerBackfillCurrentOffset = null;
    }
}
