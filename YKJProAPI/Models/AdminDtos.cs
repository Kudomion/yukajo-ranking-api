namespace YKJProAPI.Models;

public class AdminLoginRequest
{
    public string? Token { get; set; }
}

public class AdminPlayerRequest
{
    public string? Username { get; set; }
    public string? DiscordId { get; set; }
    public List<string>? Categories { get; set; }
    public bool? IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminPointsRequest
{
    public int PlayerId { get; set; }
    public string? Category { get; set; }
    // "set" = make points exactly Value; "adjust" = add Value (negative to subtract)
    public string? Mode { get; set; }
    public int Value { get; set; }
    public string? Reason { get; set; }
}

public class AdminReasonRequest
{
    public string? Reason { get; set; }
}
