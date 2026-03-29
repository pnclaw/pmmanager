using System.ComponentModel.DataAnnotations;
using pmm.Database.Common;
using Pmm.Database.Enums;

namespace Pmm.Database;

public class DownloadLog : BaseEntity
{
    public Guid Id { get; set; }

    public Guid IndexerRowId { get; set; }
    public IndexerRow IndexerRow { get; set; } = null!;

    public Guid DownloadClientId { get; set; }
    public DownloadClient DownloadClient { get; set; } = null!;

    [MaxLength(2000)]
    public string NzbName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string NzbUrl { get; set; } = string.Empty;

    /// <summary>SABnzbd nzo_id returned after the NZB was accepted.</summary>
    [MaxLength(500)]
    public string? ClientItemId { get; set; }

    public DownloadStatus Status { get; set; }

    /// <summary>Final directory path reported by the download client after extraction.</summary>
    [MaxLength(2000)]
    public string? StoragePath { get; set; }

    /// <summary>JSON array of filenames present in StoragePath after extraction.</summary>
    public string? FileNames { get; set; }

    /// <summary>Total size in bytes as reported by the download client.</summary>
    public long? TotalSizeBytes { get; set; }

    /// <summary>Bytes downloaded so far.</summary>
    public long? DownloadedBytes { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime? LastPolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
