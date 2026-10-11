using Microsoft.EntityFrameworkCore;
using YKJProAPI.Models;

namespace YKJProAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players { get; set; }

    public DbSet<Match> Matches { get; set; }

    public DbSet<Ranking> Rankings { get; set; }
}

