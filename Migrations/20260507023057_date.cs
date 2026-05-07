using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class date : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column is added defensively via ColumnExistsAsync guard in App.axaml.cs.
            // Running AddColumn here would fail with "duplicate column name" on existing DBs.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite does not reliably support DROP COLUMN; handled by guard instead.
        }
    }
}
