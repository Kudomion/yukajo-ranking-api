namespace YKJProAPI.Models;

public class DuelLog
{
    public int Id { get; set; }

    public string Player1 { get; set; } = "";

    public string Player2 { get; set; } = "";

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
