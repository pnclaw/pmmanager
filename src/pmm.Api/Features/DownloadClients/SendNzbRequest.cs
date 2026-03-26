using System.ComponentModel.DataAnnotations;

namespace pmm.Api.Features.DownloadClients;

public class SendNzbRequest
{
    [Required]
    [MaxLength(2000)]
    public string NzbUrl { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid IndexerId { get; set; }
}
