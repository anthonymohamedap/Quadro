using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferteNummer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OfferteNummer",
                table: "Offertes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OfferteNummer",
                table: "OfferteArchieven",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: bestaande offertes/archief-items behouden hun huidige (voorheen als
            // "offertenummer" getoonde) Id als doorlopend nummer. Nieuwe nummers worden vanaf nu
            // toegekend via OfferteNummering (Max over beide tabellen + 1), zodat archiveren geen
            // gat meer laat vallen in de zichtbare reeks.
#pragma warning disable EF1002
            migrationBuilder.Sql(
                "UPDATE \"Offertes\" SET \"OfferteNummer\" = \"Id\" WHERE \"OfferteNummer\" = 0;");
            migrationBuilder.Sql(
                "UPDATE \"OfferteArchieven\" SET \"OfferteNummer\" = \"OrigineleOfferteId\" WHERE \"OfferteNummer\" = 0;");
#pragma warning restore EF1002

            migrationBuilder.CreateIndex(
                name: "IX_Offertes_OfferteNummer",
                table: "Offertes",
                column: "OfferteNummer");

            migrationBuilder.CreateIndex(
                name: "IX_OfferteArchieven_OfferteNummer",
                table: "OfferteArchieven",
                column: "OfferteNummer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfferteArchieven_OfferteNummer",
                table: "OfferteArchieven");

            migrationBuilder.DropIndex(
                name: "IX_Offertes_OfferteNummer",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "OfferteNummer",
                table: "OfferteArchieven");

            migrationBuilder.DropColumn(
                name: "OfferteNummer",
                table: "Offertes");
        }
    }
}
