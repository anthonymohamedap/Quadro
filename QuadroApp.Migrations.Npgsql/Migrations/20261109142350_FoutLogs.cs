using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tijdstip = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Bron = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Melding = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Stacktrace = table.Column<string>(type: "text", nullable: false),
                    Scherm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LaatsteKlikken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AppVersie = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Computernaam = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gebruiker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsBekeken = table.Column<bool>(type: "boolean", nullable: false)
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
