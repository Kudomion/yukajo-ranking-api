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
    private readonly IConfiguration _configuration;

    public PlayerController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // POST: api/player/add
    [HttpPost("add")]
    public async Task<IActionResult> AddPlayer([FromBody] Player player, [FromHeader(Name = "X-Admin-Key")] string adminKey)
    {
        // Guard API from unauth post request:
        var expectedKey = _configuration["ADMIN_API_KEY"];

        if (string.IsNullOrEmpty(expectedKey) ||
            string.IsNullOrEmpty(adminKey) ||
            !System.Security.Cryptography.CryptographicOperations
                .FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(expectedKey),
                    System.Text.Encoding.UTF8.GetBytes(adminKey)))
        {
            return Unauthorized("Invalid admin key.");
        }

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

