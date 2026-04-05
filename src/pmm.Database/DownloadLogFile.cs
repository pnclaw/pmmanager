using System.ComponentModel.DataAnnotations;
using pmm.Database.Common;

namespace Pmm.Database;

public class DownloadLogFile : BaseEntity
{
    public Guid Id { get; set; }

    public Guid DownloadLogId { get; set; }
    public DownloadLog DownloadLog { get; set; } = null!;

    /// <summary>File path relative to the download log's StoragePath.</summary>
    [MaxLength(2000)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>OpenSubtitles hash of the file. Null if the file was too small or unreadable.</summary>
    [MaxLength(16)]
    public string? OsHash { get; set; }

    /// <summary>Perceptual hash of the video. Reserved for future implementation.</summary>
    [MaxLength(16)]
    public string? PHash { get; set; }
}
