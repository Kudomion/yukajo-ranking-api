namespace YKJProAPI.Models;

public class Match
{
    public int Id { get; set; }

    public int WinnerId { get; set; }

    public int LoserId { get; set; }

    public string Category { get; set; } = "TCG";

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
