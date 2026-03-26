using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class PrdbActorImage
{
    public Guid Id { get; set; }
    public int ImageType { get; set; }

    [MaxLength(2000)]
    public string? Url { get; set; }

    public Guid ActorId { get; set; }
    public PrdbActor Actor { get; set; } = null!;
}
