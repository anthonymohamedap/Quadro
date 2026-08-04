using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tijdstip = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Gebruiker = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntiteitType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntiteitId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Actie = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Wijzigingen = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntiteitType_EntiteitId",
                table: "AuditLogs",
                columns: new[] { "EntiteitType", "EntiteitId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Tijdstip",
                table: "AuditLogs",
                column: "Tijdstip");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
