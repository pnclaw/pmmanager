using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb;

[ApiController]
[Route("api/prdb-videos")]
[Produces("application/json")]
public class PrdbVideosController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [EndpointSummary("Get video detail")]
    [EndpointDescription("Returns full detail for a single video including images, cast, pre-names, and wanted status.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var video = await db.PrdbVideos
            .Include(v => v.Site)
            .Include(v => v.Images)
            .Include(v => v.PreNames)
            .Include(v => v.VideoActors)
                .ThenInclude(va => va.Actor)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (video is null) return NotFound();

        var wanted = await db.PrdbWantedVideos.FindAsync(id);

        return Ok(new PrdbVideoDetailResponse
        {
            Id            = video.Id,
            Title         = video.Title,
            ReleaseDate   = video.ReleaseDate,
            SiteId        = video.SiteId,
            SiteTitle     = video.Site.Title,
            SiteUrl       = video.Site.Url,
            ImageCdnPaths = video.Images
                .Where(i => i.CdnPath != null)
                .Select(i => i.CdnPath!)
                .ToList(),
            Actors = video.VideoActors
                .Select(va => new PrdbVideoDetailActorResponse
                {
                    Id   = va.Actor.Id,
                    Name = va.Actor.Name,
                })
                .OrderBy(a => a.Name)
                .ToList(),
            PreNames    = video.PreNames.Select(p => p.Title).ToList(),
            IsFulfilled = wanted?.IsFulfilled,
        });
    }
}
