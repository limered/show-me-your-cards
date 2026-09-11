using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "smyc_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Deck = table.Column<string>(type: "text", nullable: false),
                    TimerSetting = table.Column<string>(type: "text", nullable: false),
                    Closed = table.Column<bool>(type: "boolean", nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smyc_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "smyc_players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(8)", nullable: false),
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Spot = table.Column<int>(type: "integer", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smyc_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_smyc_players_smyc_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "smyc_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_smyc_players_SessionId_Spot",
                table: "smyc_players",
                columns: new[] { "SessionId", "Spot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_smyc_players_Token",
                table: "smyc_players",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "smyc_players");

            migrationBuilder.DropTable(
                name: "smyc_sessions");
        }
    }
}
