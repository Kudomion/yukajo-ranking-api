namespace YKJProAPI.Models;

// Ranked matches played by this player before ResetAt no longer count toward today's daily limit.
public class DailyLimitReset
{
    public int Id { get; set; }

    public int PlayerId { get; set; }

    public DateTime ResetAt { get; set; } = DateTime.UtcNow;
}
