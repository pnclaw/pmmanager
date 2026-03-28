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
}
