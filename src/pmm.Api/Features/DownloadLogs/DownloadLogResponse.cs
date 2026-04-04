namespace pmm.Api.Features.DownloadLogs;

public class DownloadLogResponse
{
    public Guid Id { get; set; }
    public Guid IndexerRowId { get; set; }
    public Guid DownloadClientId { get; set; }
    public string DownloadClientTitle { get; set; } = string.Empty;
    public string NzbName { get; set; } = string.Empty;
    public string NzbUrl { get; set; } = string.Empty;
    public string? ClientItemId { get; set; }

    /// <summary>Integer value of DownloadStatus enum.</summary>
    public int Status { get; set; }

    public string? StoragePath { get; set; }

    /// <summary>Filenames extracted after completion. Null while in progress.</summary>
    public List<string>? FileNames { get; set; }

    public long? TotalSizeBytes { get; set; }
    public long? DownloadedBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
