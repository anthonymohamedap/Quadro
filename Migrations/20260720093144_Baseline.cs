using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
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
                name: "GeblokkeerDagen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reden = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeblokkeerDagen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    InvalidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    Inserted = table.Column<int>(type: "INTEGER", nullable: false),
                    Updated = table.Column<int>(type: "INTEGER", nullable: false),
                    Skipped = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instellingen",
                columns: table => new
                {
                    Sleutel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Waarde = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
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
                    Opmerking = table.Column<string>(type: "TEXT", nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    Naam = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leveranciers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfferteArchieven",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrigineleOfferteId = table.Column<int>(type: "INTEGER", nullable: false),
                    KlantNaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KlantId = table.Column<int>(type: "INTEGER", nullable: true),
                    OfferteDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusOpMoment = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HadWerkBon = table.Column<bool>(type: "INTEGER", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reden = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    IsHersteld = table.Column<bool>(type: "INTEGER", nullable: false),
                    HersteldNaarOfferteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferteArchieven", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WerkBonArchieven",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrigineleWerkBonId = table.Column<int>(type: "INTEGER", nullable: false),
                    OfferteId = table.Column<int>(type: "INTEGER", nullable: false),
                    KlantNaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KlantId = table.Column<int>(type: "INTEGER", nullable: true),
                    OfferteDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OfferteStatusOpMoment = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    WerkBonStatusOpMoment = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotaalPrijsIncl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AnnuleringsReden = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    IsHersteld = table.Column<bool>(type: "INTEGER", nullable: false),
                    HersteldNaarOfferteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WerkBonArchieven", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportRowLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRowLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRowLogs_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Offertes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    KlantId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubtotaalExBtw = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BtwBedrag = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: true),
                    GeplandeDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeadlineDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GeschatteMinuten = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    KortingPct = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MeerPrijsIncl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsVoorschotBetaald = table.Column<bool>(type: "INTEGER", nullable: false),
                    VoorschotBedrag = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offertes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offertes_Klanten_KlantId",
                        column: x => x.KlantId,
                        principalTable: "Klanten",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AfwerkingsOpties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AfwerkingsGroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Volgnummer = table.Column<char>(type: "TEXT", nullable: false),
                    Kleur = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    KostprijsPerM2 = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WinstMarge = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    AfvalPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    VasteKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    LeverancierId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "LeverancierBestellingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LeverancierId = table.Column<int>(type: "INTEGER", nullable: true),
                    BestelNummer = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    BesteldOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VerwachteLeverdatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OntvangenOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AangemaaktDoor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeverancierBestellingen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
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
                    Levcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LeverancierId = table.Column<int>(type: "INTEGER", nullable: true),
                    BreedteCm = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<string>(type: "TEXT", nullable: false),
                    IsDealer = table.Column<bool>(type: "INTEGER", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: false),
                    PrijsPerMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WinstFactor = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: true),
                    AfvalPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    VasteKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    VoorraadMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    GereserveerdeVoorraadMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    InBestellingMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    InventarisKost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    LaatsteUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LaatsteVoorraadCheckOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MinimumVoorraad = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    HerbestelNiveauMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeLijsten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TypeLijsten_Leveranciers_LeverancierId",
                        column: x => x.LeverancierId,
                        principalTable: "Leveranciers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StockReservationProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
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
                name: "AfwerkingsVarianten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AfwerkingsOptieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Beschrijving = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Kleur = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    VariantCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsStandaard = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfwerkingsVarianten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfwerkingsVarianten_AfwerkingsOpties_AfwerkingsOptieId",
                        column: x => x.AfwerkingsOptieId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoorraadAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TypeLijstId = table.Column<int>(type: "INTEGER", nullable: true),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LaatstHerinnerdOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VolgendeHerinneringOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BronReferentie = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Bericht = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoorraadAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoorraadAlerts_TypeLijsten_TypeLijstId",
                        column: x => x.TypeLijstId,
                        principalTable: "TypeLijsten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Facturen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: true),
                    OfferteId = table.Column<int>(type: "INTEGER", nullable: true),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    VolgNr = table.Column<int>(type: "INTEGER", nullable: false),
                    FactuurNummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    KlantNaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KlantAdres = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    KlantBtwNummer = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    FactuurDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VervalDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeplandeDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AangenomenDoorInitialen = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsBtwVrijgesteld = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotaalExclBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VoorschotBedrag = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    KortingPct = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    KortingBedragExcl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
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
                        name: "FK_Facturen_Offertes_OfferteId",
                        column: x => x.OfferteId,
                        principalTable: "Offertes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturen_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeverancierBestelLijnen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LeverancierBestellingId = table.Column<int>(type: "INTEGER", nullable: false),
                    TypeLijstId = table.Column<int>(type: "INTEGER", nullable: false),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: true),
                    AantalMeterBesteld = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    AantalMeterOntvangen = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    RedenType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    BestelVorm = table.Column<int>(type: "INTEGER", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeverancierBestelLijnen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeverancierBestelLijnen_LeverancierBestellingen_LeverancierBestellingId",
                        column: x => x.LeverancierBestellingId,
                        principalTable: "LeverancierBestellingen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeverancierBestelLijnen_TypeLijsten_TypeLijstId",
                        column: x => x.TypeLijstId,
                        principalTable: "TypeLijsten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeverancierBestelLijnen_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OfferteRegels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OfferteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Titel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
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
                    GlasVariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    PassePartout1VariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    PassePartout2VariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiepteKernVariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    OpklevenVariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    RugVariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtraWerkMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtraPrijs = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Korting = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LegacyCode = table.Column<string>(type: "TEXT", maxLength: 6, nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
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
                        name: "FK_OfferteRegels_AfwerkingsVarianten_DiepteKernVariantId",
                        column: x => x.DiepteKernVariantId,
                        principalTable: "AfwerkingsVarianten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsVarianten_GlasVariantId",
                        column: x => x.GlasVariantId,
                        principalTable: "AfwerkingsVarianten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsVarianten_OpklevenVariantId",
                        column: x => x.OpklevenVariantId,
                        principalTable: "AfwerkingsVarianten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsVarianten_PassePartout1VariantId",
                        column: x => x.PassePartout1VariantId,
                        principalTable: "AfwerkingsVarianten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsVarianten_PassePartout2VariantId",
                        column: x => x.PassePartout2VariantId,
                        principalTable: "AfwerkingsVarianten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferteRegels_AfwerkingsVarianten_RugVariantId",
                        column: x => x.RugVariantId,
                        principalTable: "AfwerkingsVarianten",
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

            migrationBuilder.CreateTable(
                name: "WerkTaken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: false),
                    OfferteRegelId = table.Column<int>(type: "INTEGER", nullable: true),
                    GeplandVan = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeplandTot = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DuurMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    WeekNotitie = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsBesteld = table.Column<bool>(type: "INTEGER", nullable: false),
                    BestelDatum = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsOpVoorraad = table.Column<bool>(type: "INTEGER", nullable: false),
                    VoorraadStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LeverancierBestelLijnId = table.Column<int>(type: "INTEGER", nullable: true),
                    BenodigdeMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WerkTaken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WerkTaken_LeverancierBestelLijnen_LeverancierBestelLijnId",
                        column: x => x.LeverancierBestelLijnId,
                        principalTable: "LeverancierBestelLijnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WerkTaken_OfferteRegels_OfferteRegelId",
                        column: x => x.OfferteRegelId,
                        principalTable: "OfferteRegels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WerkTaken_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoorraadMutaties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TypeLijstId = table.Column<int>(type: "INTEGER", nullable: false),
                    MutatieType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AantalMeter = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    MutatieDatum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WerkBonId = table.Column<int>(type: "INTEGER", nullable: true),
                    WerkTaakId = table.Column<int>(type: "INTEGER", nullable: true),
                    LeverancierBestelLijnId = table.Column<int>(type: "INTEGER", nullable: true),
                    Referentie = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Opmerking = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoorraadMutaties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoorraadMutaties_LeverancierBestelLijnen_LeverancierBestelLijnId",
                        column: x => x.LeverancierBestelLijnId,
                        principalTable: "LeverancierBestelLijnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VoorraadMutaties_TypeLijsten_TypeLijstId",
                        column: x => x.TypeLijstId,
                        principalTable: "TypeLijsten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoorraadMutaties_WerkBonnen_WerkBonId",
                        column: x => x.WerkBonId,
                        principalTable: "WerkBonnen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VoorraadMutaties_WerkTaken_WerkTaakId",
                        column: x => x.WerkTaakId,
                        principalTable: "WerkTaken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur",
                table: "AfwerkingsOpties",
                columns: new[] { "AfwerkingsGroepId", "Volgnummer", "Kleur" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_LeverancierId",
                table: "AfwerkingsOpties",
                column: "LeverancierId");

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsVarianten_AfwerkingsOptieId_Beschrijving",
                table: "AfwerkingsVarianten",
                columns: new[] { "AfwerkingsOptieId", "Beschrijving" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_Jaar_VolgNr",
                table: "Facturen",
                columns: new[] { "Jaar", "VolgNr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_OfferteId",
                table: "Facturen",
                column: "OfferteId",
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

            migrationBuilder.CreateIndex(
                name: "IX_GeblokkeerDagen_Datum",
                table: "GeblokkeerDagen",
                column: "Datum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportRowLogs_ImportSessionId",
                table: "ImportRowLogs",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeverancierBestelLijnen_LeverancierBestellingId",
                table: "LeverancierBestelLijnen",
                column: "LeverancierBestellingId");

            migrationBuilder.CreateIndex(
                name: "IX_LeverancierBestelLijnen_TypeLijstId",
                table: "LeverancierBestelLijnen",
                column: "TypeLijstId");

            migrationBuilder.CreateIndex(
                name: "IX_LeverancierBestelLijnen_WerkBonId",
                table: "LeverancierBestelLijnen",
                column: "WerkBonId");

            migrationBuilder.CreateIndex(
                name: "IX_LeverancierBestellingen_BestelNummer",
                table: "LeverancierBestellingen",
                column: "BestelNummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeverancierBestellingen_LeverancierId",
                table: "LeverancierBestellingen",
                column: "LeverancierId");

            migrationBuilder.CreateIndex(
                name: "IX_Leveranciers_Naam",
                table: "Leveranciers",
                column: "Naam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfferteArchieven_GearchiveerdOp",
                table: "OfferteArchieven",
                column: "GearchiveerdOp");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteArchieven_Jaar",
                table: "OfferteArchieven",
                column: "Jaar");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteArchieven_OrigineleOfferteId",
                table: "OfferteArchieven",
                column: "OrigineleOfferteId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_DiepteKernId",
                table: "OfferteRegels",
                column: "DiepteKernId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_DiepteKernVariantId",
                table: "OfferteRegels",
                column: "DiepteKernVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_GlasId",
                table: "OfferteRegels",
                column: "GlasId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_GlasVariantId",
                table: "OfferteRegels",
                column: "GlasVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_OfferteId",
                table: "OfferteRegels",
                column: "OfferteId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_OpklevenId",
                table: "OfferteRegels",
                column: "OpklevenId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_OpklevenVariantId",
                table: "OfferteRegels",
                column: "OpklevenVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout1Id",
                table: "OfferteRegels",
                column: "PassePartout1Id");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout1VariantId",
                table: "OfferteRegels",
                column: "PassePartout1VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout2Id",
                table: "OfferteRegels",
                column: "PassePartout2Id");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_PassePartout2VariantId",
                table: "OfferteRegels",
                column: "PassePartout2VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_RugId",
                table: "OfferteRegels",
                column: "RugId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_RugVariantId",
                table: "OfferteRegels",
                column: "RugVariantId");

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
                name: "IX_VoorraadAlerts_TypeLijstId",
                table: "VoorraadAlerts",
                column: "TypeLijstId");

            migrationBuilder.CreateIndex(
                name: "IX_VoorraadMutaties_LeverancierBestelLijnId",
                table: "VoorraadMutaties",
                column: "LeverancierBestelLijnId");

            migrationBuilder.CreateIndex(
                name: "IX_VoorraadMutaties_TypeLijstId",
                table: "VoorraadMutaties",
                column: "TypeLijstId");

            migrationBuilder.CreateIndex(
                name: "IX_VoorraadMutaties_WerkBonId",
                table: "VoorraadMutaties",
                column: "WerkBonId");

            migrationBuilder.CreateIndex(
                name: "IX_VoorraadMutaties_WerkTaakId",
                table: "VoorraadMutaties",
                column: "WerkTaakId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonArchieven_GearchiveerdOp",
                table: "WerkBonArchieven",
                column: "GearchiveerdOp");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonArchieven_OfferteId",
                table: "WerkBonArchieven",
                column: "OfferteId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonArchieven_OrigineleWerkBonId",
                table: "WerkBonArchieven",
                column: "OrigineleWerkBonId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen",
                column: "OfferteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_GeplandVan",
                table: "WerkTaken",
                column: "GeplandVan");

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_LeverancierBestelLijnId",
                table: "WerkTaken",
                column: "LeverancierBestelLijnId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_OfferteRegelId",
                table: "WerkTaken",
                column: "OfferteRegelId");

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_WerkBonId",
                table: "WerkTaken",
                column: "WerkBonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactuurLijnen");

            migrationBuilder.DropTable(
                name: "GeblokkeerDagen");

            migrationBuilder.DropTable(
                name: "ImportRowLogs");

            migrationBuilder.DropTable(
                name: "Instellingen");

            migrationBuilder.DropTable(
                name: "OfferteArchieven");

            migrationBuilder.DropTable(
                name: "VoorraadAlerts");

            migrationBuilder.DropTable(
                name: "VoorraadMutaties");

            migrationBuilder.DropTable(
                name: "WerkBonArchieven");

            migrationBuilder.DropTable(
                name: "Facturen");

            migrationBuilder.DropTable(
                name: "ImportSessions");

            migrationBuilder.DropTable(
                name: "WerkTaken");

            migrationBuilder.DropTable(
                name: "LeverancierBestelLijnen");

            migrationBuilder.DropTable(
                name: "OfferteRegels");

            migrationBuilder.DropTable(
                name: "LeverancierBestellingen");

            migrationBuilder.DropTable(
                name: "WerkBonnen");

            migrationBuilder.DropTable(
                name: "AfwerkingsVarianten");

            migrationBuilder.DropTable(
                name: "TypeLijsten");

            migrationBuilder.DropTable(
                name: "Offertes");

            migrationBuilder.DropTable(
                name: "AfwerkingsOpties");

            migrationBuilder.DropTable(
                name: "Klanten");

            migrationBuilder.DropTable(
                name: "AfwerkingsGroepen");

            migrationBuilder.DropTable(
                name: "Leveranciers");
        }
    }
}
