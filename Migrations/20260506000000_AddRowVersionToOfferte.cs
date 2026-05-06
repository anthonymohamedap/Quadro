using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionToOfferte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite: rowversion is stored as BLOB, nullable so existing rows
            // don't need a default. EF Core SQLite provider fills it on first Save.
            // PostgreSQL: never reaches here — schema is built via EnsureCreatedAsync.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Offertes"" ADD COLUMN ""RowVersion"" BLOB NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support DROP COLUMN on older versions;
            // acceptable to leave the column when rolling back.
        }
    }
}
