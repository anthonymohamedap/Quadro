using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferteArchief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS — idempotent op fresh installs (EnsureCreatedAsync maakt tabel al aan)
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""OfferteArchieven"" (
    ""Id""                    INTEGER NOT NULL CONSTRAINT ""PK_OfferteArchieven"" PRIMARY KEY AUTOINCREMENT,
    ""OrigineleOfferteId""    INTEGER NOT NULL,
    ""KlantNaam""             TEXT    NOT NULL,
    ""KlantId""               INTEGER NULL,
    ""OfferteDatum""          TEXT    NOT NULL,
    ""Jaar""                  INTEGER NOT NULL,
    ""StatusOpMoment""        TEXT    NOT NULL,
    ""TotaalInclBtw""         TEXT    NOT NULL,
    ""HadWerkBon""            INTEGER NOT NULL,
    ""GearchiveerdOp""        TEXT    NOT NULL,
    ""Reden""                 TEXT    NULL,
    ""Snapshot""              TEXT    NOT NULL,
    ""IsHersteld""            INTEGER NOT NULL,
    ""HersteldNaarOfferteId"" INTEGER NULL
);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_GearchiveerdOp"" ON ""OfferteArchieven"" (""GearchiveerdOp"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_Jaar"" ON ""OfferteArchieven"" (""Jaar"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_OrigineleOfferteId"" ON ""OfferteArchieven"" (""OrigineleOfferteId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OfferteArchieven");
        }
    }
}
