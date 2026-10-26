using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    /// <summary>US-79 — betaalwijzen vereenvoudigd tot Bancontact, Cash, Credit card en Overschrijving (enkel data, geen schemawijziging).</summary>
    public partial class BetaalwijzenVereenvoudigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // US-79 — enkel nog Bancontact, Cash, Credit card en Overschrijving.
            // Cheque en Proton worden Cash (afspraak met Veerle, 26/10/2026).
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'Cash' WHERE \"Betaalwijze\" IN ('Kontant', 'Cheque', 'Proton');");
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'CreditCard' WHERE \"Betaalwijze\" = 'Visa';");
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'Overschrijving' WHERE \"Betaalwijze\" = 'Storting';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cheque en Proton zijn niet meer te onderscheiden van Cash: die gaan terug naar Kontant.
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'Kontant' WHERE \"Betaalwijze\" = 'Cash';");
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'Visa' WHERE \"Betaalwijze\" = 'CreditCard';");
            migrationBuilder.Sql("UPDATE \"Ontvangsten\" SET \"Betaalwijze\" = 'Storting' WHERE \"Betaalwijze\" = 'Overschrijving';");
        }
    }
}
