using System.ComponentModel.DataAnnotations;
using Pmm.Database.Enums;

namespace pmm.Api.Features.Indexers;

public class CreateIndexerRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    [Required]
    public ParsingType ParsingType { get; set; }

    public bool IsEnabled { get; set; }

    [MaxLength(2000)]
    public string ApiKey { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ApiPath { get; set; } = string.Empty;
}
