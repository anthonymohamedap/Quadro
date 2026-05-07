using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAfhaalDatumToOfferteRegel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column is added defensively via ColumnExistsAsync guard in App.axaml.cs
            // so this migration acts only as a marker in __EFMigrationsHistory.
            // Running AddColumn here would cause "duplicate column name" on existing DBs.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not needed — SQLite does not support DROP COLUMN on older versions.
        }
    }
}
