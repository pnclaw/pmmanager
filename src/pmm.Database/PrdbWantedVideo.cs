using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class PrdbWantedVideo
{
    public Guid VideoId { get; set; }

    public bool IsFulfilled { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public int? FulfilledInQuality { get; set; }

    [MaxLength(500)]
    public string? FulfillmentExternalId { get; set; }

    public int? FulfillmentByApp { get; set; }

    public DateTime PrdbCreatedAtUtc { get; set; }
    public DateTime PrdbUpdatedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public PrdbVideo? Video { get; set; }
}
