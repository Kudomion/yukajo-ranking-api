namespace YKJProAPI.Models;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    public string Actor { get; set; } = "";

    public string? ActorIp { get; set; }

    // e.g. player.create, player.update, player.deactivate, player.delete, points.set, points.adjust, match.undo, daily.reset
    public string Action { get; set; } = "";

    public string TargetType { get; set; } = "";

    public int? TargetId { get; set; }

    public string? TargetName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }
}
