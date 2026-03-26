using System.ComponentModel.DataAnnotations;
using pmm.Database.Common;
using Pmm.Database.Enums;

namespace Pmm.Database;

public class Indexer : BaseEntity
{
    public Guid Id { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    public ParsingType ParsingType { get; set; }

    public bool IsEnabled { get; set; }

    [MaxLength(2000)]
    public string ApiKey { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ApiPath { get; set; } = string.Empty;
}
