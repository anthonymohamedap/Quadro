using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddVoorraadAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS — idempotent op fresh installs (EnsureCreatedAsync maakt tabel al aan)
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""VoorraadAlerts"" (
    ""Id""                    INTEGER NOT NULL CONSTRAINT ""PK_VoorraadAlerts"" PRIMARY KEY AUTOINCREMENT,
    ""TypeLijstId""           INTEGER NULL,
    ""AlertType""             TEXT    NOT NULL,
    ""Status""                TEXT    NOT NULL,
    ""AangemaaktOp""          TEXT    NOT NULL,
    ""LaatstHerinnerdOp""     TEXT    NULL,
    ""VolgendeHerinneringOp"" TEXT    NULL,
    ""BronReferentie""        TEXT    NULL,
    ""Bericht""               TEXT    NOT NULL,
    CONSTRAINT ""FK_VoorraadAlerts_TypeLijsten_TypeLijstId""
        FOREIGN KEY (""TypeLijstId"") REFERENCES ""TypeLijsten"" (""Id"") ON DELETE SET NULL
);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VoorraadAlerts_TypeLijstId"" ON ""VoorraadAlerts"" (""TypeLijstId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VoorraadAlerts");
        }
    }
}
