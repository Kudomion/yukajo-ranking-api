namespace YKJProAPI.Models;

public class Ranking
{
    public int Id { get; set; }

    public int PlayerId { get; set; }

    public string Category { get; set; } = "TCG";

    public int Wins { get; set; }

    public int Losses { get; set; } 

    public int Points { get; set; }
}
