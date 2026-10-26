namespace YKJProAPI.Models;

public class Player
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string DiscordId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
