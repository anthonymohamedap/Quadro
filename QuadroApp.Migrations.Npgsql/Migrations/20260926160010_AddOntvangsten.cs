using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    /// <summary>US-66 — centraal ontvangstenregister (voorschotten, betalingen op bestelbon, winkelverkopen).</summary>
    public partial class AddOntvangsten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ontvangsten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Datum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Soort = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Betaalwijze = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BedragIncl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OfferteId = table.Column<int>(type: "integer", nullable: true),
                    FactuurId = table.Column<int>(type: "integer", nullable: true),
                    Omschrijving = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Aantal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrijsPerStukIncl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    KortingPct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AangemaaktDoor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AangemaaktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ontvangsten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ontvangsten_Facturen_FactuurId",
                        column: x => x.FactuurId,
                        principalTable: "Facturen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ontvangsten_Offertes_OfferteId",
                        column: x => x.OfferteId,
                        principalTable: "Offertes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ontvangsten_Datum",
                table: "Ontvangsten",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Ontvangsten_FactuurId",
                table: "Ontvangsten",
                column: "FactuurId");

            migrationBuilder.CreateIndex(
                name: "IX_Ontvangsten_OfferteId",
                table: "Ontvangsten",
                column: "OfferteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ontvangsten");
        }
    }
}
