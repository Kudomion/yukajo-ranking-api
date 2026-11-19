namespace YKJProAPI.Models;

public class Match
{
    public int Id { get; set; }

    public int WinnerId { get; set; }

    public int LoserId { get; set; }

    public string Category { get; set; } = "TCG";

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    // Points actually applied when this match was recorded (so undo is exact).
    // Matches recorded before this column existed have HasDeltas = false.
    public bool HasDeltas { get; set; }

    public int WinnerPointsDelta { get; set; }

    public int LoserPointsDelta { get; set; }

    public bool IsUndone { get; set; }

    public DateTime? UndoneAt { get; set; }

    public string? UndoReason { get; set; }
}
