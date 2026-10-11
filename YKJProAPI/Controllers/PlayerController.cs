using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Models;

namespace YKJProAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly AppDbContext _db; 

    public PlayerController(AppDbContext db)
    {
        _db = db;
    }

    // POST: api/player/add
    [HttpPost("add")]
    public async Task<IActionResult> AddPlayer([FromBody] Player player)
    {
        // Generate token automatically
        player.Token = Guid.NewGuid().ToString("N")[..8].ToUpper();
        player.CreatedAt = DateTime.UtcNow;
        _db.Players.Add(player);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Player added!", token = player.Token });
    }

    // GET: api/player/all
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var players = await _db.Players
            .Select(p => new { p.Id, p.Username, p.DiscordId, p.CreatedAt })
            .ToListAsync();
        return Ok(players);
    }
}

