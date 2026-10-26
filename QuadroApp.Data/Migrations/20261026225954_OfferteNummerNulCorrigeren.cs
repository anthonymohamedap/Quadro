using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    /// <summary>US-81 — offertes met nummer 0 krijgen een echt, uniek offertenummer (enkel data).</summary>
    public partial class OfferteNummerNulCorrigeren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // US-81 — eenmalige correctie: een opgeslagen offerte hoort nooit nummer 0 te hebben.
            // Elke rij krijgt een nummer boven alles wat ooit uitgereikt is (actief + archief).
            // "Id + max" blijft uniek, ook als de database de subquery per rij opnieuw evalueert.
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
