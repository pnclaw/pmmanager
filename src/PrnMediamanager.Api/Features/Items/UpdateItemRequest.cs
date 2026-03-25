using System.ComponentModel.DataAnnotations;

namespace PrnMediamanager.Api.Features.Items;

public class UpdateItemRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
