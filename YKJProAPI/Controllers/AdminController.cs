using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Filters;
using YKJProAPI.Models;
using YKJProAPI.Services;

namespace YKJProAPI.Controllers;

[ApiController]
[Route("api/admin")]
[AdminAuth]
public class AdminController : ControllerBase
{
    private const int MaxUsernameLength = 20;   // EDOPro nickname limit
    private const int MaxDiscordIdLength = 64;
    private const int MaxReasonLength = 300;
    private const int MaxPoints = 1_000_000;

    private readonly AppDbContext _db;
    private readonly AdminAuth _auth;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext db, AdminAuth auth, ILogger<AdminController> logger)
    {
        _db = db;
        _auth = auth;
        _logger = logger;
    }

    // ---------------------------------------------------------------- auth

    // POST api/admin/login  { "token": "<ADMIN_TOKEN>" }  -> { token, expiresAt }
    // Rate limited in Program.cs (5 per 15 min per IP).
    [HttpPost("login")]
    [SkipAdminAuth]
    public IActionResult Login([FromBody] AdminLoginRequest request)
    {
        if (!_auth.Enabled)
            return StatusCode(503, new { error = "Admin is disabled (ADMIN_TOKEN not set)." });

        if (!_auth.CheckSecret(request.Token))
        {
            _logger.LogWarning("Failed admin login from {Ip}", ClientIp);
            return Unauthorized(new { error = "Wrong admin secret." });
        }

        var (token, expiresAt) = _auth.IssueSession();
        _logger.LogInformation("Admin login from {Ip}", ClientIp);
        return Ok(new { token, expiresAt });
    }

    // GET api/admin/me -> checks the session is still valid
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { actor = _auth.ActorName });

    // ---------------------------------------------------------------- players

    // GET api/admin/players
    [HttpGet("players")]
    public async Task<IActionResult> ListPlayers()
    {
        var players = await _db.Players.OrderBy(p => p.Id).ToListAsync();
        var rankings = await _db.Rankings.ToListAsync();
        return Ok(players.Select(p => PlayerView(p, rankings.Where(r => r.PlayerId == p.Id))));
    }

    // POST api/admin/players  { username, discordId, categories: ["TCG","Anime","Community"], reason }
    [HttpPost("players")]
    public async Task<IActionResult> AddPlayer([FromBody] AdminPlayerRequest request)
    {
        var (username, err1) = ValidateUsername(request.Username);
        if (err1 != null) return BadRequest(new { error = err1 });
        var (discordId, err2) = ValidateDiscordId(request.DiscordId);
        if (err2 != null) return BadRequest(new { error = err2 });
        var (categories, err3) = ValidateCategories(request.Categories ?? RankCategories.All.ToList());
        if (err3 != null) return BadRequest(new { error = err3 });
        var (reason, err4) = ValidateReason(request.Reason, required: false);
        if (err4 != null) return BadRequest(new { error = err4 });

        if (await NameTaken(username!, excludeId: null))
            return Conflict(new { error = $"A player named \"{username}\" already exists." });

        var player = new Player
        {
            Username = username!,
            DiscordId = discordId!,
            Categories = categories!,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        Audit("player.create", "player", player.Id, player.Username, null, Snapshot(player), reason);
        await _db.SaveChangesAsync();
        return Ok(PlayerView(player, Enumerable.Empty<Ranking>()));
    }

    // PUT api/admin/players/{id}  { username?, discordId?, categories?, isActive?, reason }
    [HttpPut("players/{id:int}")]
    public async Task<IActionResult> EditPlayer(int id, [FromBody] AdminPlayerRequest request)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return NotFound(new { error = "Player not found." });

        var (reason, errR) = ValidateReason(request.Reason, required: false);
        if (errR != null) return BadRequest(new { error = errR });

        var before = Snapshot(player);

        if (request.Username != null)
        {
            var (username, err) = ValidateUsername(request.Username);
            if (err != null) return BadRequest(new { error = err });
            if (await NameTaken(username!, excludeId: id))
                return Conflict(new { error = $"A player named \"{username}\" already exists." });
            player.Username = username!;
        }
        if (request.DiscordId != null)
        {
            var (discordId, err) = ValidateDiscordId(request.DiscordId);
            if (err != null) return BadRequest(new { error = err });
            player.DiscordId = discordId!;
        }
        if (request.Categories != null)
        {
            var (categories, err) = ValidateCategories(request.Categories);
            if (err != null) return BadRequest(new { error = err });
            player.Categories = categories!;
        }
        if (request.IsActive.HasValue)
            player.IsActive = request.IsActive.Value;

        var after = Snapshot(player);
        if (after == before)
            return Ok(PlayerView(player, await _db.Rankings.Where(r => r.PlayerId == id).ToListAsync()));

        Audit("player.update", "player", player.Id, player.Username, before, after, reason);
        await _db.SaveChangesAsync();
        return Ok(PlayerView(player, await _db.Rankings.Where(r => r.PlayerId == id).ToListAsync()));
    }

    // DELETE api/admin/players/{id}?reason=...             -> deactivate (keeps history, hidden from ranking)
    // DELETE api/admin/players/{id}?permanent=true&reason=  -> delete player and their ranking rows
    [HttpDelete("players/{id:int}")]
    public async Task<IActionResult> DeletePlayer(int id, [FromQuery] bool permanent = false, [FromQuery] string? reason = null)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return NotFound(new { error = "Player not found." });

        var (r, err) = ValidateReason(reason, required: permanent);
        if (err != null) return BadRequest(new { error = err });

        await RankingRules.Lock.WaitAsync();
        try
        {
            if (!permanent)
            {
                if (!player.IsActive) return Ok(new { message = "Player is already inactive." });
                var before = Snapshot(player);
                player.IsActive = false;
                Audit("player.deactivate", "player", player.Id, player.Username, before, Snapshot(player), r);
                await _db.SaveChangesAsync();
                return Ok(new { message = "Player deactivated." });
            }

            var rankings = await _db.Rankings.Where(x => x.PlayerId == id).ToListAsync();
            var snapshot = Json(new
            {
                player = JsonSerializer.Deserialize<JsonElement>(Snapshot(player)),
                rankings = rankings.Select(x => new { x.Category, x.Wins, x.Losses, x.Points })
            });

            _db.Rankings.RemoveRange(rankings);
            _db.DailyLimitResets.RemoveRange(_db.DailyLimitResets.Where(x => x.PlayerId == id));
            _db.PendingResults.RemoveRange(_db.PendingResults.Where(x => x.Winner == player.Username || x.Loser == player.Username));
            _db.Players.Remove(player);
            Audit("player.delete", "player", id, player.Username, snapshot, null, r);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Player deleted permanently. Their past matches stay in the match list." });
        }
        finally
        {
            RankingRules.Lock.Release();
        }
    }

    // POST api/admin/players/{id}/reset-daily-limit  { reason }
    [HttpPost("players/{id:int}/reset-daily-limit")]
    public async Task<IActionResult> ResetDailyLimit(int id, [FromBody] AdminReasonRequest? request)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return NotFound(new { error = "Player not found." });

        var (reason, err) = ValidateReason(request?.Reason, required: false);
        if (err != null) return BadRequest(new { error = err });

        var start = DateTime.UtcNow.Date;
        var playedToday = await _db.Matches.CountAsync(m => !m.IsUndone && m.PlayedAt >= start && (m.WinnerId == id || m.LoserId == id));

        _db.DailyLimitResets.Add(new DailyLimitReset { PlayerId = id, ResetAt = DateTime.UtcNow });
        Audit("daily.reset", "player", id, player.Username,
            Json(new { rankedMatchesToday = playedToday }), Json(new { countsFromNow = 0 }), reason);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Daily duel limit reset for this player." });
    }

    // ---------------------------------------------------------------- points

    // POST api/admin/points  { playerId, category, mode: "set"|"adjust", value, reason }
    [HttpPost("points")]
    public async Task<IActionResult> ChangePoints([FromBody] AdminPointsRequest request)
    {
        var category = RankCategories.Normalize(request.Category);
        if (category == null) return BadRequest(new { error = "Category must be TCG, Anime or Community." });

        var mode = request.Mode?.Trim().ToLowerInvariant();
        if (mode != "set" && mode != "adjust") return BadRequest(new { error = "Mode must be \"set\" or \"adjust\"." });

        if (mode == "set" && (request.Value < 0 || request.Value > MaxPoints))
            return BadRequest(new { error = $"Points must be between 0 and {MaxPoints}." });
        if (mode == "adjust" && (request.Value == 0 || Math.Abs(request.Value) > MaxPoints))
            return BadRequest(new { error = $"Adjustment must be non-zero and at most {MaxPoints} either way." });

        var (reason, err) = ValidateReason(request.Reason, required: true);
        if (err != null) return BadRequest(new { error = err });

        var player = await _db.Players.FindAsync(request.PlayerId);
        if (player == null) return NotFound(new { error = "Player not found." });

        await RankingRules.Lock.WaitAsync();
        try
        {
            var ranking = await RankingRules.GetOrCreateRanking(_db, player.Id, category);
            var oldPoints = ranking.Points;
            var newPoints = mode == "set" ? request.Value : Math.Clamp(oldPoints + request.Value, 0, MaxPoints);
            ranking.Points = newPoints;

            Audit(mode == "set" ? "points.set" : "points.adjust", "player", player.Id, player.Username,
                Json(new { category, points = oldPoints }),
                Json(new { category, points = newPoints, requested = mode == "adjust" ? $"{request.Value:+#;-#;0}" : null }),
                reason);
            await _db.SaveChangesAsync();

            var note = mode == "adjust" && newPoints != oldPoints + request.Value ? " (capped at 0)" : "";
            return Ok(new { message = $"{player.Username} {category}: {oldPoints} -> {newPoints}{note}", oldPoints, newPoints });
        }
        finally
        {
            RankingRules.Lock.Release();
        }
    }

    // ---------------------------------------------------------------- matches

    // GET api/admin/matches?limit=50
    [HttpGet("matches")]
    public async Task<IActionResult> ListMatches([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 500);
        var matches = await _db.Matches.OrderByDescending(m => m.PlayedAt).ThenByDescending(m => m.Id).Take(limit).ToListAsync();
        var names = await PlayerNames(matches.SelectMany(m => new[] { m.WinnerId, m.LoserId }));
        return Ok(matches.Select(m => new
        {
            m.Id,
            m.PlayedAt,
            m.Category,
            m.WinnerId,
            winner = names.GetValueOrDefault(m.WinnerId, $"#{m.WinnerId} (deleted)"),
            m.LoserId,
            loser = names.GetValueOrDefault(m.LoserId, $"#{m.LoserId} (deleted)"),
            winnerPointsDelta = m.HasDeltas ? m.WinnerPointsDelta : RankingRules.WinPoints,
            loserPointsDelta = m.HasDeltas ? m.LoserPointsDelta : -RankingRules.LossPoints,
            exactPoints = m.HasDeltas,
            m.IsUndone,
            m.UndoneAt,
            m.UndoReason
        }));
    }

    // GET api/admin/pending -> first reports still waiting for the opponent's report
    [HttpGet("pending")]
    public async Task<IActionResult> ListPending()
    {
        var pending = await _db.PendingResults.OrderByDescending(p => p.ReportedAt).Take(200).ToListAsync();
        return Ok(pending.Select(p => new { p.Id, p.Winner, p.Loser, p.Category, p.ReportedAt }));
    }

    // POST api/admin/matches/{id}/undo  { reason }
    // Reverses the points/wins/losses this match applied and marks it as undone (kept for history).
    [HttpPost("matches/{id:int}/undo")]
    public async Task<IActionResult> UndoMatch(int id, [FromBody] AdminReasonRequest? request)
    {
        var (reason, err) = ValidateReason(request?.Reason, required: true);
        if (err != null) return BadRequest(new { error = err });

        await RankingRules.Lock.WaitAsync();
        try
        {
            var match = await _db.Matches.FindAsync(id);
            if (match == null) return NotFound(new { error = "Match not found." });
            if (match.IsUndone) return Conflict(new { error = "This match was already undone." });

            // Matches recorded before point tracking existed: assume the standard +100 / -30.
            var winnerDelta = match.HasDeltas ? match.WinnerPointsDelta : RankingRules.WinPoints;
            var loserDelta = match.HasDeltas ? match.LoserPointsDelta : -RankingRules.LossPoints;

            var changes = new List<object>();
            var w = await _db.Rankings.FirstOrDefaultAsync(r => r.PlayerId == match.WinnerId && r.Category == match.Category);
            if (w != null)
            {
                var old = new { w.Wins, w.Points };
                w.Wins = Math.Max(0, w.Wins - 1);
                w.Points = Math.Max(0, w.Points - winnerDelta);
                changes.Add(new { side = "winner", playerId = match.WinnerId, from = old, to = new { w.Wins, w.Points } });
            }
            var l = await _db.Rankings.FirstOrDefaultAsync(r => r.PlayerId == match.LoserId && r.Category == match.Category);
            if (l != null)
            {
                var old = new { l.Losses, l.Points };
                l.Losses = Math.Max(0, l.Losses - 1);
                l.Points = Math.Min(MaxPoints, Math.Max(0, l.Points - loserDelta));
                changes.Add(new { side = "loser", playerId = match.LoserId, from = old, to = new { l.Losses, l.Points } });
            }

            match.IsUndone = true;
            match.UndoneAt = DateTime.UtcNow;
            match.UndoReason = reason;

            var names = await PlayerNames(new[] { match.WinnerId, match.LoserId });
            Audit("match.undo", "match", match.Id,
                $"{names.GetValueOrDefault(match.WinnerId, "?")} vs {names.GetValueOrDefault(match.LoserId, "?")} ({match.Category})",
                Json(new { match.PlayedAt, winnerDelta, loserDelta, exact = match.HasDeltas }),
                Json(changes),
                reason);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Match undone and points reversed." });
        }
        finally
        {
            RankingRules.Lock.Release();
        }
    }

    // ---------------------------------------------------------------- audit

    // GET api/admin/audit?limit=100&beforeId=123
    [HttpGet("audit")]
    public async Task<IActionResult> ListAudit([FromQuery] int limit = 100, [FromQuery] int? beforeId = null)
    {
        limit = Math.Clamp(limit, 1, 500);
        var q = _db.AuditLogs.AsQueryable();
        if (beforeId.HasValue) q = q.Where(a => a.Id < beforeId.Value);
        return Ok(await q.OrderByDescending(a => a.Id).Take(limit).ToListAsync());
    }

    // ---------------------------------------------------------------- helpers

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // Audit values keep Arabic text readable (no \uXXXX escapes). They are only ever
    // rendered with textContent in the admin page, so relaxed escaping is safe there.
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, AuditJson);

    private void Audit(string action, string targetType, int? targetId, string? targetName, string? oldValue, string? newValue, string? reason)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            Actor = _auth.ActorName,
            ActorIp = ClientIp,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason
        });
    }

    private static string Snapshot(Player p) => Json(new
    {
        username = p.Username,
        discordId = p.DiscordId,
        categories = RankCategories.Parse(p.Categories),
        isActive = p.IsActive
    });

    private static object PlayerView(Player p, IEnumerable<Ranking> rankings) => new
    {
        p.Id,
        p.Username,
        p.DiscordId,
        p.IsActive,
        categories = RankCategories.Parse(p.Categories),
        p.CreatedAt,
        rankings = rankings.Select(r => new { r.Category, r.Wins, r.Losses, r.Points })
    };

    private async Task<Dictionary<int, string>> PlayerNames(IEnumerable<int> ids)
    {
        var set = ids.Distinct().ToList();
        return await _db.Players.Where(p => set.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Username);
    }

    private async Task<bool> NameTaken(string username, int? excludeId)
    {
        var names = await _db.Players.Where(p => excludeId == null || p.Id != excludeId).Select(p => p.Username).ToListAsync();
        return names.Any(n => string.Equals(n, username, StringComparison.OrdinalIgnoreCase));
    }

    private static (string? value, string? error) ValidateUsername(string? raw)
    {
        var v = raw?.Trim() ?? "";
        if (v.Length == 0) return (null, "Name is required.");
        if (v.Length > MaxUsernameLength) return (null, $"Name must be at most {MaxUsernameLength} characters.");
        if (v.Any(char.IsControl)) return (null, "Name contains invalid characters.");
        return (v, null);
    }

    private static (string? value, string? error) ValidateDiscordId(string? raw)
    {
        var v = raw?.Trim() ?? "";
        if (v.Length > MaxDiscordIdLength) return (null, $"Discord ID must be at most {MaxDiscordIdLength} characters.");
        if (v.Any(char.IsControl)) return (null, "Discord ID contains invalid characters.");
        return (v, null);
    }

    private static (string? csv, string? error) ValidateCategories(List<string> raw)
    {
        var list = new List<string>();
        foreach (var c in raw)
        {
            var n = RankCategories.Normalize(c);
            if (n == null) return (null, $"Unknown category \"{c}\". Use TCG, Anime or Community.");
            if (!list.Contains(n)) list.Add(n);
        }
        if (list.Count == 0) return (null, "Pick at least one category.");
        return (RankCategories.ToCsv(list), null);
    }

    private static (string? value, string? error) ValidateReason(string? raw, bool required)
    {
        var v = raw?.Trim() ?? "";
        if (required && v.Length == 0) return (null, "A reason is required.");
        if (v.Length > MaxReasonLength) return (null, $"Reason must be at most {MaxReasonLength} characters.");
        if (v.Any(c => char.IsControl(c) && c != '\n')) return (null, "Reason contains invalid characters.");
        return (v.Length == 0 ? null : v, null);
    }
}
