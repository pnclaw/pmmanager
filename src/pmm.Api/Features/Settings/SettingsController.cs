using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Settings;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Get application settings")]
    [EndpointDescription("Returns the current application settings.")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var settings = await db.AppSettings.FirstAsync();
        return Ok(ToResponse(settings));
    }

    [HttpPut]
    [Consumes("application/json")]
    [EndpointSummary("Update application settings")]
    [EndpointDescription("Updates the application settings.")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request)
    {
        var settings = await db.AppSettings.FirstAsync();

        settings.PrdbApiKey = request.PrdbApiKey;
        settings.PrdbApiUrl = request.PrdbApiUrl;
        settings.PreferredVideoQuality = request.PreferredVideoQuality;

        await db.SaveChangesAsync();
        return Ok(ToResponse(settings));
    }

    private static SettingsResponse ToResponse(AppSettings settings) => new()
    {
        PrdbApiKey = settings.PrdbApiKey,
        PrdbApiUrl = settings.PrdbApiUrl,
        PreferredVideoQuality = (int)settings.PreferredVideoQuality,
    };
}
