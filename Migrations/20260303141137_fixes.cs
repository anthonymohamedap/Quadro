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
            // Facturen en FactuurLijnen werden oorspronkelijk via EnsureCreated aangemaakt
            // en waren niet aanwezig in InitialClean. Dit herstel zorgt dat een frisse
            // database ook correct aangemaakt wordt via migrations.
            // IF NOT EXISTS is veilig voor databases die de tabellen al hadden.

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Facturen"" (
    ""Id""                      INTEGER NOT NULL CONSTRAINT ""PK_Facturen"" PRIMARY KEY AUTOINCREMENT,
    ""WerkBonId""               INTEGER NULL,
    ""OfferteId""               INTEGER NULL,
    ""Jaar""                    INTEGER NOT NULL DEFAULT 0,
    ""VolgNr""                  INTEGER NOT NULL DEFAULT 0,
    ""FactuurNummer""           TEXT    NOT NULL DEFAULT '',
    ""DocumentType""            TEXT    NOT NULL DEFAULT 'Bestelbon',
    ""KlantNaam""               TEXT    NOT NULL DEFAULT '',
    ""KlantAdres""              TEXT    NULL,
    ""KlantBtwNummer""          TEXT    NULL,
    ""FactuurDatum""            TEXT    NOT NULL DEFAULT (datetime('now')),
    ""VervalDatum""             TEXT    NOT NULL DEFAULT (datetime('now')),
    ""Opmerking""               TEXT    NULL,
    ""AangenomenDoorInitialen"" TEXT    NULL,
    ""IsBtwVrijgesteld""        INTEGER NOT NULL DEFAULT 0,
    ""TotaalExclBtw""           TEXT    NOT NULL DEFAULT '0',
    ""TotaalBtw""               TEXT    NOT NULL DEFAULT '0',
    ""TotaalInclBtw""           TEXT    NOT NULL DEFAULT '0',
    ""VoorschotBedrag""         TEXT    NOT NULL DEFAULT '0',
    ""ExportPad""               TEXT    NULL,
    ""Status""                  TEXT    NOT NULL DEFAULT 'Draft',
    ""AangemaaktOp""            TEXT    NOT NULL DEFAULT (datetime('now')),
    ""BijgewerktOp""            TEXT    NULL,
    ""RowVersion""              BLOB    NULL,
    CONSTRAINT ""FK_Facturen_WerkBonnen_WerkBonId""
        FOREIGN KEY (""WerkBonId"") REFERENCES ""WerkBonnen"" (""Id""),
    CONSTRAINT ""FK_Facturen_Offertes_OfferteId""
        FOREIGN KEY (""OfferteId"") REFERENCES ""Offertes"" (""Id"")
);");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Facturen_WerkBonId""
    ON ""Facturen"" (""WerkBonId"") WHERE ""WerkBonId"" IS NOT NULL;");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Facturen_OfferteId""
    ON ""Facturen"" (""OfferteId"") WHERE ""OfferteId"" IS NOT NULL;");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Facturen_Jaar_VolgNr""
    ON ""Facturen"" (""Jaar"", ""VolgNr"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""FactuurLijnen"" (
    ""Id""           INTEGER NOT NULL CONSTRAINT ""PK_FactuurLijnen"" PRIMARY KEY AUTOINCREMENT,
    ""FactuurId""    INTEGER NOT NULL,
    ""Omschrijving"" TEXT    NOT NULL DEFAULT '',
    ""Aantal""       TEXT    NOT NULL DEFAULT '0',
    ""Eenheid""      TEXT    NOT NULL DEFAULT 'm²',
    ""PrijsExcl""    TEXT    NOT NULL DEFAULT '0',
    ""BtwPct""       TEXT    NOT NULL DEFAULT '0',
    ""TotaalExcl""   TEXT    NOT NULL DEFAULT '0',
    ""TotaalBtw""    TEXT    NOT NULL DEFAULT '0',
    ""TotaalIncl""   TEXT    NOT NULL DEFAULT '0',
    ""Sortering""    INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT ""FK_FactuurLijnen_Facturen_FactuurId""
        FOREIGN KEY (""FactuurId"") REFERENCES ""Facturen"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_FactuurLijnen_FactuurId""
    ON ""FactuurLijnen"" (""FactuurId"");");
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
