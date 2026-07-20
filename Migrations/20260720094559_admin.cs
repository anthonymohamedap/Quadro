using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class admin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gebruikers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GebruikersNaam = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VolledigeNaam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WachtwoordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false),
                    MoetWachtwoordWijzigen = table.Column<bool>(type: "INTEGER", nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LaatsteLogin = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gebruikers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gebruikers_GebruikersNaam",
                table: "Gebruikers",
                column: "GebruikersNaam",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gebruikers");
        }
    }
}
