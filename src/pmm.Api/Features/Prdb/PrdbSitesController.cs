using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb;

[ApiController]
[Route("api/prdb-sites")]
[Produces("application/json")]
public class PrdbSitesController(AppDbContext db, PrdbFavoritesService favoritesService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List prdb sites")]
    [EndpointDescription("Returns all synced prdb sites with video counts. Optionally filter by search term or favorites.")]
    [ProducesResponseType(typeof(IEnumerable<PrdbSiteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? favoritesOnly)
    {
        var q = db.PrdbSites
            .Include(s => s.Network)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => EF.Functions.Like(s.Title, $"%{search}%"));

        if (favoritesOnly == true)
            q = q.Where(s => s.IsFavorite);

        var sites = await q
            .OrderBy(s => s.Title)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.Url,
                s.NetworkId,
                NetworkTitle = s.Network != null ? s.Network.Title : null,
                s.IsFavorite,
                s.FavoritedAtUtc,
                VideoCount = s.Videos.Count,
            })
            .ToListAsync();

        return Ok(sites.Select(s => new PrdbSiteResponse
        {
            Id           = s.Id,
            Title        = s.Title,
            Url          = s.Url,
            NetworkId    = s.NetworkId,
            NetworkTitle = s.NetworkTitle,
            IsFavorite   = s.IsFavorite,
            FavoritedAtUtc = s.FavoritedAtUtc,
            VideoCount   = s.VideoCount,
        }));
    }

    [HttpPost("{id:guid}/favorite")]
    [EndpointSummary("Add a favorite site")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite(Guid id, CancellationToken ct)
    {
        if (!await db.PrdbSites.AnyAsync(s => s.Id == id)) return NotFound();
        await favoritesService.SetSiteFavoriteAsync(id, true, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/favorite")]
    [EndpointSummary("Remove a favorite site")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken ct)
    {
        if (!await db.PrdbSites.AnyAsync(s => s.Id == id)) return NotFound();
        await favoritesService.SetSiteFavoriteAsync(id, false, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/videos")]
    [EndpointSummary("List videos for a site")]
    [EndpointDescription("Returns all synced videos for the given site including pre-names and actor count.")]
    [ProducesResponseType(typeof(IEnumerable<PrdbVideoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideos(Guid id, [FromQuery] string? search)
    {
        if (!await db.PrdbSites.AnyAsync(s => s.Id == id)) return NotFound();

        var q = db.PrdbVideos
            .Where(v => v.SiteId == id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(v => EF.Functions.Like(v.Title, $"%{search}%"));

        var videos = await q
            .OrderByDescending(v => v.ReleaseDate)
            .Select(v => new PrdbVideoResponse
            {
                Id          = v.Id,
                Title       = v.Title,
                ReleaseDate = v.ReleaseDate,
                ActorCount  = v.VideoActors.Count,
                PreNames    = v.PreNames
                    .Select(p => new PrdbPreNameResponse { Id = p.Id, Title = p.Title })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(videos);
    }

}
