using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Models;
using YKJProAPI.Services;

namespace YKJProAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RankingController> _logger;

    public RankingController(AppDbContext db, IConfiguration configuration, ILogger<RankingController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    // POST match result: api/ranking/result
    //
    // Both players' clients send the same result after a ranked duel.
    // The first report is stored as pending; the matching second report records the match once.
    // The game client looks for these substrings in the response, so keep them stable:
    //   "Daily duel limit", "pending", "confirmed".
    [HttpPost("result")]
    public async Task<IActionResult> PostResult([FromBody] MatchResultRequest request)
    {
        // Validate Client Secret (proves request came from the game client):
        var expectedSecret = _configuration["CLIENT_SECRET"];
        if (string.IsNullOrEmpty(expectedSecret) || !SecretEquals(expectedSecret, request.ClientSecret))
            return Unauthorized("Invalid Client Secret.");

        if (string.IsNullOrEmpty(request.Category) || request.Category == "None")
            return Ok(new { message = "Unranked Match - Not Recorded" });

        var category = RankCategories.Normalize(request.Category);
        if (category == null)
            return BadRequest("Invalid category.");

        // Find winner and loser by username (exact in-game nickname):
        var winner = await _db.Players.FirstOrDefaultAsync(p => p.Username == request.Winner && p.IsActive);
        var loser = await _db.Players.FirstOrDefaultAsync(p => p.Username == request.Loser && p.IsActive);

        if (winner == null || loser == null)
            return BadRequest("One or both players not found in community database.");

        if (winner.Id == loser.Id)
            return BadRequest("Winner and loser must be different players.");

        foreach (var p in new[] { winner, loser })
            if (!RankCategories.Parse(p.Categories).Contains(category))
                return BadRequest($"Player {p.Username} is not registered for {category} ranking.");

        int dailyLimit = int.TryParse(_configuration["DAILY_DUEL_LIMIT"], out var dl) && dl > 0 ? dl : 4;
        int windowMinutes = int.TryParse(_configuration["RESULT_CONFIRM_MINUTES"], out var wm) && wm > 0 ? wm : 5;
        var now = DateTime.UtcNow;

        await RankingRules.Lock.WaitAsync();
        try
        {
            // Drop unconfirmed reports that are too old to be confirmed.
            var cutoff = now.AddMinutes(-windowMinutes);
            await _db.PendingResults.Where(p => p.ReportedAt < cutoff).ExecuteDeleteAsync();

            // Daily duel limit between these two players (any category):
            int todayCount = await RankingRules.CountDuelsInWindow(_db, winner.Id, loser.Id);
            if (todayCount >= dailyLimit)
                return BadRequest($"Daily duel limit ({dailyLimit}) reached between {winner.Username} and {loser.Username}.");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var pending = await _db.PendingResults
                .Where(p => p.Winner == winner.Username && p.Loser == loser.Username && p.Category == category)
                .OrderBy(p => p.ReportedAt)
                .FirstOrDefaultAsync();

            if (pending == null)
            {
                _db.PendingResults.Add(new PendingResult
                {
                    Winner = winner.Username,
                    Loser = loser.Username,
                    Category = category,
                    ReportedAt = now,
                    ReporterIp = ip
                });
                await _db.SaveChangesAsync();
                return Ok(new { message = "Result pending: waiting for opponent confirmation." });
            }

            if (pending.ReporterIp != null && pending.ReporterIp == ip)
                _logger.LogInformation("Result {Winner} > {Loser} confirmed from the same IP as the first report.", winner.Username, loser.Username);

            _db.PendingResults.Remove(pending);

            var (winnerDelta, loserDelta) = await RankingRules.ApplyMatch(_db, winner.Id, loser.Id, category);
            _db.Matches.Add(new Match
            {
                WinnerId = winner.Id,
                LoserId = loser.Id,
                Category = category,
                PlayedAt = now,
                HasDeltas = true,
                WinnerPointsDelta = winnerDelta,
                LoserPointsDelta = loserDelta
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Result confirmed and recorded." });
        }
        finally
        {
            RankingRules.Lock.Release();
        }
    }

    // GET duelist ranking -> api/ranking/tcg
    [HttpGet("{category}")]
    public async Task<IActionResult> GetRanking(string category)
    {
        var canonical = RankCategories.Normalize(category);

        var rows = await _db.Rankings
            .Where(r => r.Category.ToLower() == category.ToLower())
            .Join(_db.Players.Where(p => p.IsActive), r => r.PlayerId, p => p.Id,
                (r, p) => new { p.Username, p.Categories, r.Wins, r.Losses, r.Points })
            .ToListAsync();

        // Hide players an admin removed from this category; ties broken by wins, then fewer losses, then name.
        var ranked = rows
            .Where(r => canonical == null || RankCategories.Parse(r.Categories).Contains(canonical))
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Wins)
            .ThenBy(r => r.Losses)
            .ThenBy(r => r.Username, StringComparer.Ordinal)
            .Select((r, i) => new
            {
                rank = i + 1,
                name = r.Username,
                wins = r.Wins,
                losses = r.Losses,
                points = r.Points,
            });

        return Ok(ranked);
    }

    private static bool SecretEquals(string expected, string? actual)
    {
        if (actual == null) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
    }
}
