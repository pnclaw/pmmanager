using System.ComponentModel.DataAnnotations;
using Pmm.Database.Enums;

namespace Pmm.Database;

public class AppSettings
{
    public int Id { get; set; } = 1;

    [MaxLength(255)]
    public string PrdbApiKey { get; set; } = string.Empty;

    [MaxLength(255)]
    public string PrdbApiUrl { get; set; } = "https://api.prdb.net";

    public VideoQuality PreferredVideoQuality { get; set; } = VideoQuality.P2160;

    /// <summary>
    /// Next page to fetch during actor backfill. Null means the backfill is complete.
    /// </summary>
    public int? PrdbActorSyncPage { get; set; } = 1;

    /// <summary>
    /// Set when the backfill completes. Used as the CreatedAfter cursor for new-actor checks.
    /// </summary>
    public DateTime? PrdbActorLastSyncedAt { get; set; }

    /// <summary>
    /// Total actor count on prdb as of last backfill page. Used for progress display.
    /// </summary>
    public int? PrdbActorTotalCount { get; set; }

    /// <summary>
    /// Set at the end of each successful SyncWorker run. Used to calculate next scheduled run.
    /// </summary>
    public DateTime? SyncWorkerLastRunAt { get; set; }

    /// <summary>
    /// Set at the end of each successful wanted-video sync run. Used for status display.
    /// </summary>
    public DateTime? PrdbWantedVideoLastSyncedAt { get; set; }

    /// <summary>
    /// When true, images from the prdb API are blurred in the UI.
    /// </summary>
    public bool SafeForWork { get; set; }

    /// <summary>
    /// Set at the end of each successful indexer-row match run. Used for status display.
    /// </summary>
    public DateTime? IndexerRowMatchLastRunAt { get; set; }

    /// <summary>
    /// Next page to fetch during prename backfill. Not null means backfill is in progress.
    /// Null means backfill is complete — check PrenamesSyncCursorUtc for incremental state.
    /// </summary>
    public int? PrenamesBackfillPage { get; set; }

    /// <summary>
    /// Total prename count on prdb as of the last backfill page. Used for progress display.
    /// </summary>
    public int? PrenamesBackfillTotalCount { get; set; }

    /// <summary>
    /// Set when backfill completes. Used as CreatedFrom cursor for incremental sync thereafter.
    /// Null means backfill has never completed.
    /// </summary>
    public DateTime? PrenamesSyncCursorUtc { get; set; }
}
