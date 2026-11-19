using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YKJProAPI.Models;

namespace YKJProAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players { get; set; }

    public DbSet<Match> Matches { get; set; }

    public DbSet<Ranking> Rankings { get; set; }

    // Legacy table, no longer written. Kept so existing databases stay untouched.
    public DbSet<DuelLog> DuelLogs { get; set; }

    public DbSet<PendingResult> PendingResults { get; set; }

    public DbSet<AuditLog> AuditLogs { get; set; }

    public DbSet<DailyLimitReset> DailyLimitResets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>().Property(p => p.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<Player>().Property(p => p.Categories).HasDefaultValue(Services.RankCategories.AllCsv);
        modelBuilder.Entity<Player>().HasIndex(p => p.Username);

        modelBuilder.Entity<Ranking>().HasIndex(r => new { r.PlayerId, r.Category });

        modelBuilder.Entity<Match>().HasIndex(m => m.PlayedAt);

        modelBuilder.Entity<PendingResult>().HasIndex(p => p.ReportedAt);

        modelBuilder.Entity<AuditLog>().HasIndex(a => a.At);

        modelBuilder.Entity<DailyLimitReset>().HasIndex(d => d.PlayerId);

        // All times are stored as UTC; SQLite loses DateTimeKind, so restore it on read
        // (JSON then carries a "Z" and browsers show the right local time).
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
            }
    }
}
