using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    Naam = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfwerkingsGroepen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tijdstip = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Gebruiker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntiteitType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntiteitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Actie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Wijzigingen = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeblokkeerDagen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Datum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reden = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeblokkeerDagen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gebruikers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GebruikersNaam = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VolledigeNaam = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WachtwoordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    IsActief = table.Column<bool>(type: "boolean", nullable: false),
                    MoetWachtwoordWijzigen = table.Column<bool>(type: "boolean", nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LaatsteLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gebruikers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    Inserted = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instellingen",
                columns: table => new
                {
                    Sleutel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Waarde = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instellingen", x => x.Sleutel);
                });

            migrationBuilder.CreateTable(
                name: "Klanten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Voornaam = table.Column<string>(type: "text", nullable: false),
                    Achternaam = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Telefoon = table.Column<string>(type: "text", nullable: true),
                    Straat = table.Column<string>(type: "text", nullable: true),
                    Nummer = table.Column<string>(type: "text", nullable: true),
                    Postcode = table.Column<string>(type: "text", nullable: true),
                    Gemeente = table.Column<string>(type: "text", nullable: true),
                    BtwNummer = table.Column<string>(type: "text", nullable: true),
                    Opmerking = table.Column<string>(type: "text", nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Klanten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leveranciers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Naam = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leveranciers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfferteArchieven",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrigineleOfferteId = table.Column<int>(type: "integer", nullable: false),
                    KlantNaam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KlantId = table.Column<int>(type: "integer", nullable: true),
                    OfferteDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Jaar = table.Column<int>(type: "integer", nullable: false),
                    StatusOpMoment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HadWerkBon = table.Column<bool>(type: "boolean", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reden = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    IsHersteld = table.Column<bool>(type: "boolean", nullable: false),
                    HersteldNaarOfferteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferteArchieven", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WerkBonArchieven",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrigineleWerkBonId = table.Column<int>(type: "integer", nullable: false),
                    OfferteId = table.Column<int>(type: "integer", nullable: false),
                    KlantNaam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KlantId = table.Column<int>(type: "integer", nullable: true),
                    OfferteDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OfferteStatusOpMoment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WerkBonStatusOpMoment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotaalPrijsIncl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GearchiveerdOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnnuleringsReden = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    IsHersteld = table.Column<bool>(type: "boolean", nullable: false),
                    HersteldNaarOfferteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WerkBonArchieven", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportRowLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportSessionId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IssuesJson = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    KlantId = table.Column<int>(type: "integer", nullable: true),
                    SubtotaalExBtw = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BtwBedrag = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Datum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Opmerking = table.Column<string>(type: "text", nullable: true),
                    GeplandeDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadlineDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeschatteMinuten = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    KortingPct = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MeerPrijsIncl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsVoorschotBetaald = table.Column<bool>(type: "boolean", nullable: false),
                    VoorschotBedrag = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AfwerkingsGroepId = table.Column<int>(type: "integer", nullable: false),
                    Naam = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Volgnummer = table.Column<char>(type: "character(1)", nullable: false),
                    Kleur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    KostprijsPerM2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    WinstMarge = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    AfvalPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VasteKost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "integer", nullable: false),
                    LeverancierId = table.Column<int>(type: "integer", nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeverancierId = table.Column<int>(type: "integer", nullable: true),
                    BestelNummer = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BesteldOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerwachteLeverdatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OntvangenOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Opmerking = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AangemaaktDoor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Artikelnummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Levcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LeverancierId = table.Column<int>(type: "integer", nullable: true),
                    BreedteCm = table.Column<int>(type: "integer", nullable: false),
                    Soort = table.Column<string>(type: "text", nullable: false),
                    IsDealer = table.Column<bool>(type: "boolean", nullable: false),
                    Opmerking = table.Column<string>(type: "text", nullable: false),
                    PrijsPerMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    WinstFactor = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    AfvalPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    VasteKost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    WerkMinuten = table.Column<int>(type: "integer", nullable: false),
                    VoorraadMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    GereserveerdeVoorraadMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    InBestellingMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    InventarisKost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    LaatsteUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LaatsteVoorraadCheckOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinimumVoorraad = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    HerbestelNiveauMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OfferteId = table.Column<int>(type: "integer", nullable: false),
                    AfhaalDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotaalPrijsIncl = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StockReservationProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AfwerkingsOptieId = table.Column<int>(type: "integer", nullable: false),
                    Beschrijving = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kleur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VariantCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsStandaard = table.Column<bool>(type: "boolean", nullable: false),
                    IsActief = table.Column<bool>(type: "boolean", nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeLijstId = table.Column<int>(type: "integer", nullable: true),
                    AlertType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LaatstHerinnerdOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VolgendeHerinneringOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BronReferentie = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Bericht = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WerkBonId = table.Column<int>(type: "integer", nullable: true),
                    OfferteId = table.Column<int>(type: "integer", nullable: true),
                    Jaar = table.Column<int>(type: "integer", nullable: false),
                    VolgNr = table.Column<int>(type: "integer", nullable: false),
                    FactuurNummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    KlantNaam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KlantAdres = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    KlantBtwNummer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FactuurDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VervalDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeplandeDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Opmerking = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AangenomenDoorInitialen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsBtwVrijgesteld = table.Column<bool>(type: "boolean", nullable: false),
                    TotaalExclBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VoorschotBedrag = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    KortingPct = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    KortingBedragExcl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExportPad = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AangemaaktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BijgewerktOp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeverancierBestellingId = table.Column<int>(type: "integer", nullable: false),
                    TypeLijstId = table.Column<int>(type: "integer", nullable: false),
                    WerkBonId = table.Column<int>(type: "integer", nullable: true),
                    AantalMeterBesteld = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AantalMeterOntvangen = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    RedenType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BestelVorm = table.Column<int>(type: "integer", nullable: false),
                    Opmerking = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeverancierBestelLijnen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeverancierBestelLijnen_LeverancierBestellingen_Leverancier~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OfferteId = table.Column<int>(type: "integer", nullable: false),
                    Opmerking = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AantalStuks = table.Column<int>(type: "integer", nullable: false),
                    BreedteCm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HoogteCm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InlegBreedteCm = table.Column<decimal>(type: "numeric", nullable: true),
                    InlegHoogteCm = table.Column<decimal>(type: "numeric", nullable: true),
                    TypeLijstId = table.Column<int>(type: "integer", nullable: true),
                    GlasId = table.Column<int>(type: "integer", nullable: true),
                    PassePartout1Id = table.Column<int>(type: "integer", nullable: true),
                    PassePartout2Id = table.Column<int>(type: "integer", nullable: true),
                    DiepteKernId = table.Column<int>(type: "integer", nullable: true),
                    OpklevenId = table.Column<int>(type: "integer", nullable: true),
                    RugId = table.Column<int>(type: "integer", nullable: true),
                    GlasVariantId = table.Column<int>(type: "integer", nullable: true),
                    PassePartout1VariantId = table.Column<int>(type: "integer", nullable: true),
                    PassePartout2VariantId = table.Column<int>(type: "integer", nullable: true),
                    DiepteKernVariantId = table.Column<int>(type: "integer", nullable: true),
                    OpklevenVariantId = table.Column<int>(type: "integer", nullable: true),
                    RugVariantId = table.Column<int>(type: "integer", nullable: true),
                    ExtraWerkMinuten = table.Column<int>(type: "integer", nullable: false),
                    ExtraPrijs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Korting = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LegacyCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    AfhaalDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AfgesprokenPrijsExcl = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotaalExcl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SubtotaalExBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BtwBedrag = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FactuurId = table.Column<int>(type: "integer", nullable: false),
                    Omschrijving = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Aantal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Eenheid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrijsExcl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BtwPct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TotaalExcl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotaalIncl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Sortering = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WerkBonId = table.Column<int>(type: "integer", nullable: false),
                    OfferteRegelId = table.Column<int>(type: "integer", nullable: true),
                    GeplandVan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeplandTot = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DuurMinuten = table.Column<int>(type: "integer", nullable: false),
                    Omschrijving = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    WeekNotitie = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsBesteld = table.Column<bool>(type: "boolean", nullable: false),
                    BestelDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsOpVoorraad = table.Column<bool>(type: "boolean", nullable: false),
                    VoorraadStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LeverancierBestelLijnId = table.Column<int>(type: "integer", nullable: true),
                    BenodigdeMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeLijstId = table.Column<int>(type: "integer", nullable: false),
                    MutatieType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AantalMeter = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MutatieDatum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WerkBonId = table.Column<int>(type: "integer", nullable: true),
                    WerkTaakId = table.Column<int>(type: "integer", nullable: true),
                    LeverancierBestelLijnId = table.Column<int>(type: "integer", nullable: true),
                    Referentie = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Opmerking = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoorraadMutaties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoorraadMutaties_LeverancierBestelLijnen_LeverancierBestelL~",
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
                name: "IX_AuditLogs_EntiteitType_EntiteitId",
                table: "AuditLogs",
                columns: new[] { "EntiteitType", "EntiteitId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Tijdstip",
                table: "AuditLogs",
                column: "Tijdstip");

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_FactuurNummer",
                table: "Facturen",
                column: "FactuurNummer",
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
                name: "IX_Gebruikers_GebruikersNaam",
                table: "Gebruikers",
                column: "GebruikersNaam",
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
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "FactuurLijnen");

            migrationBuilder.DropTable(
                name: "GeblokkeerDagen");

            migrationBuilder.DropTable(
                name: "Gebruikers");

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
