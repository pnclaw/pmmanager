using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class PrdbVideo
{
    public Guid Id { get; set; }

    [MaxLength(1000)]
    public string Title { get; set; } = string.Empty;

    public DateOnly? ReleaseDate { get; set; }

    public Guid SiteId { get; set; }
    public PrdbSite Site { get; set; } = null!;

    public DateTime PrdbCreatedAtUtc { get; set; }
    public DateTime PrdbUpdatedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public ICollection<PrdbVideoImage> Images { get; set; } = [];
    public ICollection<PrdbVideoPreName> PreNames { get; set; } = [];
}
