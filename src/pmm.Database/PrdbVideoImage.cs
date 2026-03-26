using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class PrdbVideoImage
{
    public Guid Id { get; set; }

    [MaxLength(2000)]
    public string? CdnPath { get; set; }

    public Guid VideoId { get; set; }
    public PrdbVideo Video { get; set; } = null!;
}
