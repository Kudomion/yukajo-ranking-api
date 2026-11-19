using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Models;

namespace YKJProAPI.Services;

public static class RankingRules
{
    public const int WinPoints = 100;
    public const int LossPoints = 30;

    // Serializes every change to rankings / matches / pending results.
    // Both clients post the result at the same moment; without this they could both
    // see "no pending result" and neither would confirm. The API runs as one instance.
    public static readonly SemaphoreSlim Lock = new(1, 1);

    public static async Task<Ranking> GetOrCreateRanking(AppDbContext db, int playerId, string category)
    {
        var ranking = db.Rankings.Local.FirstOrDefault(r => r.PlayerId == playerId && r.Category == category)
            ?? await db.Rankings.FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Category == category);
        if (ranking == null)
        {
            ranking = new Ranking { PlayerId = playerId, Category = category };
            db.Rankings.Add(ranking);
        }
        return ranking;
    }

    // Applies a win/loss and returns the points actually added to the winner and
    // removed from the loser (negative, may be less than LossPoints because of the 0 floor).
    public static async Task<(int winnerDelta, int loserDelta)> ApplyMatch(AppDbContext db, int winnerId, int loserId, string category)
    {
        var w = await GetOrCreateRanking(db, winnerId, category);
        var l = await GetOrCreateRanking(db, loserId, category);

        w.Wins++;
        w.Points += WinPoints;

        l.Losses++;
        var before = l.Points;
        l.Points = Math.Max(0, l.Points - LossPoints);

        return (WinPoints, l.Points - before);
    }

    // Start of the window counted for the daily limit between two players:
    // today's UTC midnight, or a later admin reset for either player.
    public static async Task<DateTime> DailyWindowStart(AppDbContext db, int p1, int p2)
    {
        var start = DateTime.UtcNow.Date;
        var lastReset = await db.DailyLimitResets
            .Where(r => (r.PlayerId == p1 || r.PlayerId == p2) && r.ResetAt >= start)
            .OrderByDescending(r => r.ResetAt)
            .Select(r => (DateTime?)r.ResetAt)
            .FirstOrDefaultAsync();
        return lastReset ?? start;
    }

    public static async Task<int> CountDuelsInWindow(AppDbContext db, int p1, int p2)
    {
        var start = await DailyWindowStart(db, p1, p2);
        return await db.Matches.CountAsync(m =>
            !m.IsUndone && m.PlayedAt >= start &&
            ((m.WinnerId == p1 && m.LoserId == p2) || (m.WinnerId == p2 && m.LoserId == p1)));
    }
}
