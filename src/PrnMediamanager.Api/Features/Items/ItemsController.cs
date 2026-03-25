using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrnMediamanager.Api.Infrastructure;

namespace PrnMediamanager.Api.Features.Items;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ItemsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/items
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Items
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/items/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.Items.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    // POST /api/items
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest request)
    {
        var item = new Item
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    // PUT /api/items/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemRequest request)
    {
        var item = await _db.Items.FindAsync(id);
        if (item is null) return NotFound();

        item.Name = request.Name;
        item.Description = request.Description;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // DELETE /api/items/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Items.FindAsync(id);
        if (item is null) return NotFound();

        _db.Items.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
