using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Models;

namespace YKJProAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public RankingController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // POST match result: api/ranking/result
    [HttpPost("result")]
    public async Task<IActionResult> PostResult([FromBody] MatchResultRequest request)
    {
        // Find winner and loser by username:
        var winner = await _db.Players.FirstOrDefaultAsync(p => p.Username == request.Winner);
        var loser = await _db.Players.FirstOrDefaultAsync(p => p.Username == request.Loser);

        if (winner == null || loser == null)
            return BadRequest("One or both players not found in community database.");

        // Token Validation () => Replaced By Client Secret below:
        /*if (winner.Token != request.Cli && loser.Token != request.Token)
            return Unauthorized("Invalid Community Token.");*/

        // Validate Client Secret (proves request came from your game):
        var expectedSecret = _configuration["CLIENT_SECRET"];
        if (request.ClientSecret != expectedSecret)
            return Unauthorized("Invalid Client Secret.");
        
        if (string.IsNullOrEmpty(request.Category) || request.Category == "None")
            return Ok(new { message = "Unranked Match - Not Recorded" });

        // Daily duel limit check:
        int dailyLimit = int.Parse(_configuration["DAILY_DUEL_LIMIT"] ?? "4");
        var today = DateTime.UtcNow.Date;

        string p1 = winner.Username;
        string p2 = loser.Username;

        // Count duels between these two players today
        int todayCount = await _db.DuelLogs.CountAsync(d =>
            ((d.Player1 == p1 && d.Player2 == p2) ||
             (d.Player1 == p2 && d.Player2 == p1)) &&
            d.PlayedAt.Date == today);

        if (todayCount >= dailyLimit)
            return BadRequest($"Daily duel limit ({dailyLimit}) reached between {p1} and {p2}.");

        // Save match:
        var match = new Match()
        {
            WinnerId = winner.Id,
            LoserId = loser.Id,
            Category = request.Category,
            PlayedAt = DateTime.UtcNow
        };
        await _db.Matches.AddAsync(match);

        // Update Rankings:
        await UpdateRanking(winner.Id, request.Category, true);
        await UpdateRanking(loser.Id, request.Category, false);

        await _db.SaveChangesAsync();
        return Ok(new { message =  "Match recorded successfully!" });
    }

    // GET duelist ranking -> api/ranking/tcg
    [HttpGet("{category}")]
    public async Task<IActionResult> GetRanking(string category)
    {
        var rankings = await _db.Rankings
            .Where(r => r.Category.ToLower() == category.ToLower())
            .OrderByDescending(r => r.Points)
            .Join(_db.Players, r => r.PlayerId, p => p.Id,
            (r, p) => new
            {
                rank = 0,
                name = p.Username,
                wins = r.Wins,
                losses = r.Losses,
                points = r.Points
            })
            .ToListAsync();

        // Adding Ranking Numbers:
        var ranked = rankings.Select((r, i) => new
        {
            rank = i + 1,
            r.name,
            r.wins,
            r.losses,
            r.points,
        });

        return Ok(ranked);
    }


    private async Task UpdateRanking(int playerId, string category, bool isWin)
    {
        var ranking = await _db.Rankings
            .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Category == category);

        if (ranking == null)
        {
            ranking = new Ranking()
            {
                PlayerId = playerId,
                Category = category,

            };
            _db.Rankings.Add(ranking);
        }

        if (isWin)
        {
            ranking.Wins++;
            ranking.Points += 100;
        }
        else
        {
            ranking.Losses++;

            // -30 points dedcuted per loss
            ranking.Points = Math.Max(0, ranking.Points - 30);
        }
    }
}
