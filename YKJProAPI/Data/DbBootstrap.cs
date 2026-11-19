using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace YKJProAPI.Data;

public static class DbBootstrap
{
    // Older deployments created the database with EnsureCreated(), which leaves no
    // __EFMigrationsHistory table, so Migrate() would try to recreate existing tables.
    // Detect that case, mark the migrations whose schema is already present as applied,
    // then run the remaining migrations normally.
    public static void MigrateDatabase(AppDbContext db, ILogger logger)
    {
        var conn = db.Database.GetDbConnection();
        conn.Open();
        try
        {
            if (TableExists(conn, "Players") && !TableExists(conn, "__EFMigrationsHistory"))
            {
                logger.LogWarning("Database has no migration history; adopting existing schema.");
                Exec(conn, "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)");

                var baseline = new List<string> { "20260920140920_InitialCreate" };
                if (TableExists(conn, "DuelLogs")) baseline.Add("20261018184143_AddDuelLogLimit");
                if (!ColumnExists(conn, "Players", "Token")) baseline.Add("20261026201059_RemovePlayerToken");

                foreach (var id in baseline)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, '9.0.0')";
                    AddParam(cmd, "$id", id);
                    cmd.ExecuteNonQuery();
                }
                logger.LogWarning("Marked as applied: {Migrations}", string.Join(", ", baseline));
            }
        }
        finally
        {
            conn.Close();
        }

        db.Database.Migrate();
    }

    private static bool TableExists(DbConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n";
        AddParam(cmd, "$n", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static bool ColumnExists(DbConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info($t) WHERE name = $c";
        AddParam(cmd, "$t", table);
        AddParam(cmd, "$c", column);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static void Exec(DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
