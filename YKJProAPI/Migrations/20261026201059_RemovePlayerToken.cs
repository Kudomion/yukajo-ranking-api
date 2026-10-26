using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YKJProAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Token",
                table: "Players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
