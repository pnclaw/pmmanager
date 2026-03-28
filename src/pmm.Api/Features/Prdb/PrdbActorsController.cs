using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb;

[ApiController]
[Route("api/prdb-actors")]
[Produces("application/json")]
public class PrdbActorsController(AppDbContext db, PrdbFavoritesService favoritesService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List prdb actors")]
    [EndpointDescription("Returns all synced prdb actors with aliases. Optionally filter by search term.")]
    [ProducesResponseType(typeof(IEnumerable<PrdbActorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? favoritesOnly)
    {
        var q = db.PrdbActors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a => EF.Functions.Like(a.Name, $"%{search}%"));

        if (favoritesOnly == true)
            q = q.Where(a => a.IsFavorite);

        var actors = await q
            .OrderBy(a => a.Name)
            .Select(a => new PrdbActorResponse
            {
                Id             = a.Id,
                Name           = a.Name,
                Gender         = a.Gender,
                Nationality    = a.Nationality,
                Birthday       = a.Birthday,
                IsFavorite     = a.IsFavorite,
                FavoritedAtUtc = a.FavoritedAtUtc,
                Aliases        = a.Aliases.Select(x => x.Name).ToList(),
            })
            .ToListAsync();

        return Ok(actors);
    }

    [HttpPost("{id:guid}/favorite")]
    [EndpointSummary("Add a favorite actor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite(Guid id, CancellationToken ct)
    {
        if (!await db.PrdbActors.AnyAsync(a => a.Id == id)) return NotFound();
        await favoritesService.SetActorFavoriteAsync(id, true, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/favorite")]
    [EndpointSummary("Remove a favorite actor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken ct)
    {
        if (!await db.PrdbActors.AnyAsync(a => a.Id == id)) return NotFound();
        await favoritesService.SetActorFavoriteAsync(id, false, ct);
        return NoContent();
    }

}
