using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pmm.Database;

namespace pmm.Api.Features.Indexers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IndexersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List all indexers")]
    [EndpointDescription("Returns all indexers ordered by creation date descending.")]
    [ProducesResponseType(typeof(IEnumerable<IndexerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var indexers = await db.Indexers
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return Ok(indexers.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get indexer by ID")]
    [EndpointDescription("Returns a single indexer by its unique identifier.")]
    [ProducesResponseType(typeof(IndexerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var indexer = await db.Indexers.FindAsync(id);
        return indexer is null ? NotFound() : Ok(ToResponse(indexer));
    }

    [HttpPost]
    [Consumes("application/json")]
    [EndpointSummary("Create indexer")]
    [EndpointDescription("Creates a new indexer and returns the created resource.")]
    [ProducesResponseType(typeof(IndexerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIndexerRequest request)
    {
        var indexer = new Indexer
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Url = request.Url,
            ParsingType = request.ParsingType,
            IsEnabled = request.IsEnabled,
            ApiKey = request.ApiKey,
            ApiPath = request.ApiPath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Indexers.Add(indexer);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = indexer.Id }, ToResponse(indexer));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [EndpointSummary("Update indexer")]
    [EndpointDescription("Updates an existing indexer by its unique identifier.")]
    [ProducesResponseType(typeof(IndexerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIndexerRequest request)
    {
        var indexer = await db.Indexers.FindAsync(id);
        if (indexer is null) return NotFound();

        indexer.Title = request.Title;
        indexer.Url = request.Url;
        indexer.ParsingType = request.ParsingType;
        indexer.IsEnabled = request.IsEnabled;
        indexer.ApiKey = request.ApiKey;
        indexer.ApiPath = request.ApiPath;
        indexer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToResponse(indexer));
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete indexer")]
    [EndpointDescription("Deletes an indexer by its unique identifier.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var indexer = await db.Indexers.FindAsync(id);
        if (indexer is null) return NotFound();

        db.Indexers.Remove(indexer);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IndexerResponse ToResponse(Indexer indexer) => new()
    {
        Id = indexer.Id,
        Title = indexer.Title,
        Url = indexer.Url,
        ParsingType = (int)indexer.ParsingType,
        IsEnabled = indexer.IsEnabled,
        ApiKey = indexer.ApiKey,
        ApiPath = indexer.ApiPath,
        CreatedAt = indexer.CreatedAt,
        UpdatedAt = indexer.UpdatedAt,
    };
}
