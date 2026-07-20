using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class AddTypeLijstPerLijstPricingDropStaaflijst : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema already applied to database — no-op to sync migration history.
            migrationBuilder.Sql("SELECT 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStaaflijst",
                table: "TypeLijsten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE TypeLijsten SET IsStaaflijst = 1 WHERE WinstFactor IS NOT NULL OR AfvalPercentage IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "WinstFactor",
                table: "TypeLijsten");

            migrationBuilder.DropColumn(
                name: "AfvalPercentage",
                table: "TypeLijsten");

            migrationBuilder.Sql(
                "INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'StaaflijstWinstFactor', '3.5' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'StaaflijstWinstFactor');");
            migrationBuilder.Sql(
                "INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'StaaflijstAfvalPercentage', '20' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'StaaflijstAfvalPercentage');");
        }
    }
}
