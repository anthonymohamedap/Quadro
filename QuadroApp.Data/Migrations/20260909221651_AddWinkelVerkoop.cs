using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddWinkelVerkoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WinkelVerkopen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Aantal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PrijsInclBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BtwPct = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Betaalwijze = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WinkelVerkopen", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WinkelVerkopen");
        }
    }
}
