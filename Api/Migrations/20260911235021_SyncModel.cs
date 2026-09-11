using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        // ponytail: no-op. Prior migrations already created Postgres column types;
        // this migration exists only to realign the model snapshot (scaffolded under
        // SQLite) with Npgsql. The generated AlterColumn ops were spurious SQLite->PG
        // retypes that fail on the already-correct Postgres schema.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
