using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.DownloadClients;
using Pmm.Database;

namespace pmm.Api.Features.DownloadLogs;

[ApiController]
[Route("api/download-logs")]
[Produces("application/json")]
public class DownloadLogsController(AppDbContext db, DownloadPollService pollService) : ControllerBase
{
    [HttpPost("poll")]
    [EndpointSummary("Poll download clients")]
    [EndpointDescription("Immediately polls all download clients for status updates, identical to the scheduled background tick.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Poll(CancellationToken ct)
    {
        await pollService.PollAsync(ct);
        return NoContent();
    }

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

    [HttpDelete("failed")]
    [EndpointSummary("Delete failed download logs")]
    [EndpointDescription("Permanently removes all download log entries with a Failed status.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFailed(CancellationToken ct)
    {
        await db.DownloadLogs
            .Where(l => l.Status == Pmm.Database.Enums.DownloadStatus.Failed)
            .ExecuteDeleteAsync(ct);
        return NoContent();
    }

    [HttpDelete]
    [EndpointSummary("Delete all download logs")]
    [EndpointDescription("Permanently removes all download log entries.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        await db.DownloadLogs.ExecuteDeleteAsync(ct);
        return NoContent();
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
