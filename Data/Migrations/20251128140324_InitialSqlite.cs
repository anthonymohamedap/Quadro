using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AfwerkingsGroepen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<char>(type: "TEXT", maxLength: 1, nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfwerkingsGroepen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instellingen",
                columns: table => new
                {
                    Sleutel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Waarde = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instellingen", x => x.Sleutel);
                });

            migrationBuilder.CreateTable(
                name: "Klanten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Voornaam = table.Column<string>(type: "TEXT", nullable: false),
                    Achternaam = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", nullable: true),
                    Straat = table.Column<string>(type: "TEXT", nullable: true),
                    Nummer = table.Column<string>(type: "TEXT", nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", nullable: true),
                    Gemeente = table.Column<string>(type: "TEXT", nullable: true),
                    BtwNummer = table.Column<string>(type: "TEXT", nullable: true),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Klanten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leveranciers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leveranciers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Offertes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KlantId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubtotaalExBtw = table.Column<decimal>(type: "decimal(18,2)", precision: 10, scale: 2, nullable: false),
                    BtwBedrag = table.Column<decimal>(type: "decimal(18,2)", precision: 10, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "decimal(18,2)", precision: 10, scale: 2, nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offertes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offertes_Klanten_KlantId",
                        column: x => x.KlantId,
                        principalTable: "Klanten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AfwerkingsOpties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AfwerkingsGroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: false),
                    KostprijsPerM2 = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WinstMarge = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    AfvalPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    VasteKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    LeverancierId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfwerkingsOpties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfwerkingsOpties_AfwerkingsGroepen_AfwerkingsGroepId",
                        column: x => x.AfwerkingsGroepId,
                        principalTable: "AfwerkingsGroepen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AfwerkingsOpties_Leveranciers_LeverancierId",
                        column: x => x.LeverancierId,
                        principalTable: "Leveranciers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TypeLijsten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Artikelnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LeverancierCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    LeverancierId = table.Column<int>(type: "INTEGER", nullable: false),
                    BreedteCm = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<string>(type: "TEXT", nullable: false),
                    Serie = table.Column<string>(type: "TEXT", nullable: true),
                    IsDealer = table.Column<bool>(type: "INTEGER", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: false),
                    PrijsPerMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WinstMargeFactor = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    AfvalPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    VasteKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    VoorraadMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    InventarisKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    LaatsteUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MinimumVoorraad = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeLijsten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TypeLijsten_Leveranciers_LeverancierId",
                        column: x => x.LeverancierId,
                        principalTable: "Leveranciers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WerkBonnen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OfferteId = table.Column<int>(type: "INTEGER", nullable: false),
                    AfhaalDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotaalPrijsIncl = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WerkBonnen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WerkBonnen_Offertes_OfferteId",
                        column: x => x.OfferteId,
                        principalTable: "Offertes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfferteRegels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OfferteId = table.Column<int>(type: "INTEGER", nullable: false),
                    AantalStuks = table.Column<int>(type: "INTEGER", nullable: false),
                    BreedteCm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HoogteCm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InlegBreedteCm = table.Column<decimal>(type: "TEXT", nullable: true),
                    InlegHoogteCm = table.Column<decimal>(type: "TEXT", nullable: true),
                    TypeLijstId = table.Column<int>(type: "INTEGER", nullable: true),
                    GlasId = table.Column<int>(type: "INTEGER", nullable: true),
                    PassePartout1Id = table.Column<int>(type: "INTEGER", nullable: true),
                    PassePartout2Id = table.Column<int>(type: "INTEGER", nullable: true),
                    DiepteKernId = table.Column<int>(type: "INTEGER", nullable: true),
                    OpklevenId = table.Column<int>(type: "INTEGER", nullable: true),
                    RugId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtraWerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtraPrijs = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Korting = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LegacyCode = table.Column<string>(type: "TEXT", maxLength: 6, nullable: true),
                    AfgesprokenPrijsExcl = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotaalExcl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubtotaalExBtw = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BtwBedrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferteRegels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_DiepteKernId",
                        column: x => x.DiepteKernId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_GlasId",
                        column: x => x.GlasId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_OpklevenId",
                        column: x => x.OpklevenId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_PassePartout1Id",
                        column: x => x.PassePartout1Id,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_PassePartout2Id",
                        column: x => x.PassePartout2Id,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsOpties_RugId",
                        column: x => x.RugId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_Offertes_OfferteId",
                        column: x => x.OfferteId,
                        principalTable: "Offertes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfferteRegels_TypeLijsten_TypeLijstId",
                        column: x => x.TypeLijstId,
                        principalTable: "TypeLijsten",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WerkTaken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: false),
                    GeplandVan = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeplandTot = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DuurMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WerkTaken", x => x.Id);
                    table.CheckConstraint("CK_WerkTaak_Duur_Positive", "[DuurMinuten] >= 1");
                    table.CheckConstraint("CK_WerkTaak_Tot_After_Van", "[GeplandTot] > [GeplandVan]");
                    table.ForeignKey(
                        name: "FK_WerkTaken_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer",
                table: "AfwerkingsOpties",
                columns: new[] { "AfwerkingsGroepId", "Volgnummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_LeverancierId",
                table: "AfwerkingsOpties",
                column: "LeverancierId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_DiepteKernId",
                table: "OfferteRegels",
                column: "DiepteKernId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_GlasId",
                table: "OfferteRegels",
                column: "GlasId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_OfferteId",
                table: "OfferteRegels",
                column: "OfferteId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_OpklevenId",
                table: "OfferteRegels",
                column: "OpklevenId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout1Id",
                table: "OfferteRegels",
                column: "PassePartout1Id");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout2Id",
                table: "OfferteRegels",
                column: "PassePartout2Id");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_RugId",
                table: "OfferteRegels",
                column: "RugId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_TypeLijstId",
                table: "OfferteRegels",
                column: "TypeLijstId");

            migrationBuilder.CreateIndex(
                name: "IX_Offertes_KlantId",
                table: "Offertes",
                column: "KlantId");

            migrationBuilder.CreateIndex(
                name: "IX_TypeLijsten_LeverancierId",
                table: "TypeLijsten",
                column: "LeverancierId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen",
                column: "OfferteId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_GeplandVan",
                table: "WerkTaken",
                column: "GeplandVan");

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_WerkBonId",
                table: "WerkTaken",
                column: "WerkBonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Instellingen");

            migrationBuilder.DropTable(
                name: "OfferteRegels");

            migrationBuilder.DropTable(
                name: "WerkTaken");

            migrationBuilder.DropTable(
                name: "AfwerkingsOpties");

            migrationBuilder.DropTable(
                name: "TypeLijsten");

            migrationBuilder.DropTable(
                name: "WerkBonnen");

            migrationBuilder.DropTable(
                name: "AfwerkingsGroepen");

            migrationBuilder.DropTable(
                name: "Leveranciers");

            migrationBuilder.DropTable(
                name: "Offertes");

            migrationBuilder.DropTable(
                name: "Klanten");
        }
    }
}
