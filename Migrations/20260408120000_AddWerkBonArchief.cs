using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddWerkBonArchief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS — idempotent op fresh installs (EnsureCreatedAsync maakt tabel al aan)
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""WerkBonArchieven"" (
    ""Id""                     INTEGER NOT NULL CONSTRAINT ""PK_WerkBonArchieven"" PRIMARY KEY AUTOINCREMENT,
    ""OrigineleWerkBonId""     INTEGER NOT NULL,
    ""OfferteId""              INTEGER NOT NULL,
    ""KlantNaam""              TEXT    NOT NULL,
    ""KlantId""                INTEGER NULL,
    ""OfferteDatum""           TEXT    NOT NULL,
    ""OfferteStatusOpMoment""  TEXT    NOT NULL,
    ""WerkBonStatusOpMoment""  TEXT    NOT NULL,
    ""TotaalPrijsIncl""        TEXT    NOT NULL,
    ""GearchiveerdOp""         TEXT    NOT NULL,
    ""AnnuleringsReden""       TEXT    NULL,
    ""Snapshot""               TEXT    NOT NULL,
    ""IsHersteld""             INTEGER NOT NULL,
    ""HersteldNaarOfferteId""  INTEGER NULL
);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_GearchiveerdOp""  ON ""WerkBonArchieven"" (""GearchiveerdOp"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OrigineleWerkBonId"" ON ""WerkBonArchieven"" (""OrigineleWerkBonId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OfferteId"" ON ""WerkBonArchieven"" (""OfferteId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WerkBonArchieven");
        }
    }
}
