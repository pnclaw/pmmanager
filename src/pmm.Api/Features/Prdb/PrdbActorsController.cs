using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Prdb;

[ApiController]
[Route("api/prdb-actors")]
[Produces("application/json")]
public class PrdbActorsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List prdb actors")]
    [EndpointDescription("Returns all synced prdb actors with aliases. Optionally filter by search term.")]
    [ProducesResponseType(typeof(IEnumerable<PrdbActorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var q = db.PrdbActors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a => EF.Functions.Like(a.Name, $"%{search}%"));

        var actors = await q
            .OrderBy(a => a.Name)
            .Select(a => new PrdbActorResponse
            {
                Id          = a.Id,
                Name        = a.Name,
                Gender      = a.Gender,
                Nationality = a.Nationality,
                Birthday    = a.Birthday,
                Aliases     = a.Aliases.Select(x => x.Name).ToList(),
            })
            .ToListAsync();

        return Ok(actors);
    }

    [HttpPost("sync")]
    [EndpointSummary("Sync actors from prdb (stub)")]
    [EndpointDescription("Placeholder — sync implementation pending.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Sync()
    {
        return Ok(new { message = "Sync not yet implemented." });
    }
}
