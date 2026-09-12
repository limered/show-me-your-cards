using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SmycRound : Migration
    {
        // ponytail: scaffolded under SQLite, so it originally retyped every PG column
        // to TEXT/INTEGER. Only the two AddColumn ops are real.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Revealed",
                table: "smyc_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Card",
                table: "smyc_players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revealed",
                table: "smyc_sessions");

            migrationBuilder.DropColumn(
                name: "Card",
                table: "smyc_players");
        }
    }
}
