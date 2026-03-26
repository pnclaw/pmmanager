using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class PrdbVideoPreName
{
    public Guid Id { get; set; }

    [MaxLength(1000)]
    public string Title { get; set; } = string.Empty;

    public Guid VideoId { get; set; }
    public PrdbVideo Video { get; set; } = null!;
}
