using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.DownloadLogs;

[ApiController]
[Route("api/download-logs")]
[Produces("application/json")]
public class DownloadLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List download logs")]
    [ProducesResponseType(typeof(IEnumerable<DownloadLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var logs = await db.DownloadLogs
            .Include(l => l.DownloadClient)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(logs.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get download log by ID")]
    [ProducesResponseType(typeof(DownloadLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var log = await db.DownloadLogs
            .Include(l => l.DownloadClient)
            .FirstOrDefaultAsync(l => l.Id == id);

        return log is null ? NotFound() : Ok(ToResponse(log));
    }

    private static DownloadLogResponse ToResponse(DownloadLog log) => new()
    {
        Id                   = log.Id,
        IndexerRowId         = log.IndexerRowId,
        DownloadClientId     = log.DownloadClientId,
        DownloadClientTitle  = log.DownloadClient.Title,
        NzbName              = log.NzbName,
        NzbUrl               = log.NzbUrl,
        ClientItemId         = log.ClientItemId,
        Status               = (int)log.Status,
        StoragePath          = log.StoragePath,
        FileNames            = log.FileNames != null
            ? JsonSerializer.Deserialize<List<string>>(log.FileNames)
            : null,
        TotalSizeBytes       = log.TotalSizeBytes,
        DownloadedBytes      = log.DownloadedBytes,
        ErrorMessage         = log.ErrorMessage,
        LastPolledAt         = log.LastPolledAt,
        CompletedAt          = log.CompletedAt,
        CreatedAt            = log.CreatedAt,
        UpdatedAt            = log.UpdatedAt,
    };
}
