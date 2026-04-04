using System.ComponentModel.DataAnnotations;
using Pmm.Database.Enums;

namespace pmm.Api.Features.Settings;

public class UpdateSettingsRequest
{
    [MaxLength(255)]
    public string PrdbApiKey { get; set; } = string.Empty;

    [MaxLength(255)]
    public string PrdbApiUrl { get; set; } = string.Empty;

    public VideoQuality PreferredVideoQuality { get; set; }

    public bool SafeForWork { get; set; }

    [Range(1, 3650)]
    public int IndexerBackfillDays { get; set; } = 30;
}
