using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class PgVersieGelijktijdigWijzigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A-04: Offerte en WerkTaak krijgen, zoals Bestelbon, TypeLijst en WerkBon, de systeemkolom xmin als
            // concurrency-token (eigenschap PgVersie). xmin bestaat op elke PostgreSQL-tabel al; de Npgsql-generator
            // slaat deze AddColumn daarom over (nagekeken op PostgreSQL 16 op een bestaande database). Enkel model-metadata.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "WerkTaken",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Offertes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "WerkTaken");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Offertes");
        }
    }
}
