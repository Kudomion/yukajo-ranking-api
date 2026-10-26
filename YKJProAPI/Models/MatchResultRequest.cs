namespace YKJProAPI.Models;

public class MatchResultRequest
{
    public string Winner { get; set; } = "";

    public string Loser { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string Category { get; set; } = "TCG";
}
