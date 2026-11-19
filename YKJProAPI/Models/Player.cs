namespace YKJProAPI.Models;

public class Player
{
    public int Id { get; set; }

    // In-game nickname. Must match exactly what the game client sends.
    public string Username { get; set; } = "";

    public string DiscordId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Deactivated players are hidden from rankings and cannot record matches.
    public bool IsActive { get; set; } = true;

    // Comma-separated canonical categories the player may be ranked in, e.g. "TCG,Anime,Community".
    public string Categories { get; set; } = Services.RankCategories.AllCsv;
}
