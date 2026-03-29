using System.ComponentModel.DataAnnotations;

namespace Pmm.Database;

public class IndexerRowMatch
{
    public Guid Id { get; set; }

    public Guid IndexerRowId { get; set; }
    public IndexerRow IndexerRow { get; set; } = null!;

    public Guid PrdbVideoId { get; set; }
    public PrdbVideo Video { get; set; } = null!;

    public Guid MatchedPreNameId { get; set; }
    public PrdbVideoPreName MatchedPreName { get; set; } = null!;

    /// <summary>
    /// The prename title that triggered the match (denormalised for convenience).
    /// </summary>
    [MaxLength(1000)]
    public string MatchedTitle { get; set; } = string.Empty;

    public DateTime MatchedAtUtc { get; set; }
}
