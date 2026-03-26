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
}
