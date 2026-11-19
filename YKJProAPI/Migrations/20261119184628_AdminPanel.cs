using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YKJProAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdminPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categories",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: "TCG,Anime,Community");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDeltas",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUndone",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoserPointsDelta",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UndoReason",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UndoneAt",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerPointsDelta",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    ActorIp = table.Column<string>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetName = table.Column<string>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyLimitResets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResetAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyLimitResets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Winner = table.Column<string>(type: "TEXT", nullable: false),
                    Loser = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReporterIp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rankings_PlayerId_Category",
                table: "Rankings",
                columns: new[] { "PlayerId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Username",
                table: "Players",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayedAt",
                table: "Matches",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_At",
                table: "AuditLogs",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_DailyLimitResets_PlayerId",
                table: "DailyLimitResets",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingResults_ReportedAt",
                table: "PendingResults",
                column: "ReportedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DailyLimitResets");

            migrationBuilder.DropTable(
                name: "PendingResults");

            migrationBuilder.DropIndex(
                name: "IX_Rankings_PlayerId_Category",
                table: "Rankings");

            migrationBuilder.DropIndex(
                name: "IX_Players_Username",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Matches_PlayedAt",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Categories",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HasDeltas",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "IsUndone",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LoserPointsDelta",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UndoReason",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UndoneAt",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "WinnerPointsDelta",
                table: "Matches");
        }
    }
}
