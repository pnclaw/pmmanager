namespace pmm.Api.Features.Settings;

public class SettingsResponse
{
    public string PrdbApiKey { get; set; } = string.Empty;
    public string PrdbApiUrl { get; set; } = string.Empty;

    /// <summary>Preferred video quality integer value (0 = 720p, 1 = 1080p, 2 = 2160p).</summary>
    public int PreferredVideoQuality { get; set; }

    public bool SafeForWork { get; set; }
}
