using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AfvalPercentage",
                table: "AfwerkingsOpties",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Facturen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    VolgNr = table.Column<int>(type: "INTEGER", nullable: false),
                    FactuurNummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    KlantNaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KlantAdres = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    KlantBtwNummer = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    FactuurDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VervalDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AangenomenDoorInitialen = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsBtwVrijgesteld = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotaalExclBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExportPad = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturen_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactuurLijnen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FactuurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Aantal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Eenheid = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PrijsExcl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BtwPct = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    TotaalExcl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalIncl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Sortering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactuurLijnen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactuurLijnen_Facturen_FactuurId",
                        column: x => x.FactuurId,
                        principalTable: "Facturen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_Jaar_VolgNr",
                table: "Facturen",
                columns: new[] { "Jaar", "VolgNr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_WerkBonId",
                table: "Facturen",
                column: "WerkBonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactuurLijnen_FactuurId",
                table: "FactuurLijnen",
                column: "FactuurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactuurLijnen");

            migrationBuilder.DropTable(
                name: "Facturen");

            migrationBuilder.DropColumn(
                name: "AfvalPercentage",
                table: "AfwerkingsOpties");
        }
    }
}
