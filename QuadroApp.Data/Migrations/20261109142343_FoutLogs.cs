using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class FoutLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoutLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tijdstip = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Bron = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Melding = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Stacktrace = table.Column<string>(type: "TEXT", nullable: false),
                    Scherm = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LaatsteKlikken = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    AppVersie = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Computernaam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Gebruiker = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsBekeken = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoutLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoutLogs_Hash",
                table: "FoutLogs",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoutLogs_Tijdstip",
                table: "FoutLogs",
                column: "Tijdstip");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoutLogs");
        }
    }
}
