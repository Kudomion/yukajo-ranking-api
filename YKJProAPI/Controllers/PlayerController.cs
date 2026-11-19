using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;

namespace YKJProAPI.Controllers;

// Adding/editing players moved to the admin API (/api/admin/players).
[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlayerController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/player/all  (public: active players only, no Discord IDs)
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var players = await _db.Players
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Username, p.CreatedAt })
            .ToListAsync();
        return Ok(players);
    }
}
