using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Fix: een offerte die met nummer 0 in het archief zat (van vóór US-81) kwam bij herstellen terug
    /// met nummer 0 ("Werkbon 0"). Zelfde eenmalige correctie als OfferteNummerNulCorrigeren (enkel data).
    /// </summary>
    public partial class OfferteNummerNulNaArchief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Elke offerte met nummer 0 krijgt een nummer boven alles wat ooit uitgereikt is (actief + archief);
            // "Id + max" blijft uniek. Gearchiveerde rijen met nummer 0 krijgen bij herstellen voortaan een nieuw nummer.
            migrationBuilder.Sql(
                "UPDATE \"Offertes\" SET \"OfferteNummer\" = \"Id\" + (" +
                "SELECT MAX(n) FROM (" +
                "SELECT COALESCE(MAX(\"OfferteNummer\"), 0) AS n FROM \"Offertes\" " +
                "UNION ALL SELECT COALESCE(MAX(\"OfferteNummer\"), 0) AS n FROM \"OfferteArchieven\") t" +
                ") WHERE \"OfferteNummer\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Niet terug te draaien: de oorspronkelijke 0-nummers zijn betekenisloos.
        }
    }
}
