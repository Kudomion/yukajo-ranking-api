using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;

namespace YKJProAPI.Services;

// A copy of the rankings database every day, next to it (DB_PATH folder)/backups/yukajo-YYYYMMDD.db.
// The last 7 are kept. On Railway that folder is the volume, so backups survive redeploys.
// To restore: stop the service, copy a backup over yukajo.db, start it again.
public sealed class DailyBackup(IServiceScopeFactory scopes, IConfiguration config, ILogger<DailyBackup> log) : BackgroundService
{
    private const int Keep = 7;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stop);   // let the service start first
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await BackUpIfDueAsync(stop); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Daily backup failed (it will try again in an hour)");
            }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }

    private async Task BackUpIfDueAsync(CancellationToken stop)
    {
        string dbPath = Path.GetFullPath(string.IsNullOrWhiteSpace(config["DB_PATH"]) ? "yukajo.db" : config["DB_PATH"]!);
        string folder = Path.Combine(Path.GetDirectoryName(dbPath)!, "backups");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"yukajo-{DateTime.UtcNow:yyyyMMdd}.db");
        if (File.Exists(file)) return;   // today's is done

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // VACUUM INTO makes a clean, consistent copy while the service keeps running.
            // (The path is ours, not user input; quotes are doubled anyway.)
            await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{file.Replace("'", "''")}'", stop);
        }
        log.LogInformation("Rankings backup saved: {File}", file);

        foreach (var old in Directory.GetFiles(folder, "yukajo-*.db").OrderByDescending(f => f).Skip(Keep))
            File.Delete(old);
    }
}
