namespace YKJProAPI.Models;

// First report of a ranked duel, waiting for the opponent's client to send the same result.
public class PendingResult
{
    public int Id { get; set; }

    public string Winner { get; set; } = "";

    public string Loser { get; set; } = "";

    public string Category { get; set; } = "TCG";

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    public string? ReporterIp { get; set; }
}
