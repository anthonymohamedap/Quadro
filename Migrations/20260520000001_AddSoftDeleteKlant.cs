using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteKlant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite ondersteunt ADD COLUMN met DEFAULT — geen table rebuild nodig.
            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "Klanten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "GearchiveerdOp",
                table: "Klanten",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite 3.35+ ondersteunt DROP COLUMN.
            // Bij oudere SQLite-versies faalt dit — gebruik dan een backup.
            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "Klanten");

            migrationBuilder.DropColumn(
                name: "GearchiveerdOp",
                table: "Klanten");
        }
    }
}
