using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Fix: opslaan van een bestaande offerte zette het offertenummer op 0 ("Werkbon 0"). Zelfde eenmalige
    /// correctie als OfferteNummerNulCorrigeren voor de offertes die zo op 0 kwamen (enkel data).
    /// </summary>
    public partial class OfferteNummerNulNaOpslaan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
            // Niet terug te draaien: de 0-nummers zijn betekenisloos.
        }
    }
}
