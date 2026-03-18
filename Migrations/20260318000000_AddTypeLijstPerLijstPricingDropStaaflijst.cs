using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class AddTypeLijstPerLijstPricingDropStaaflijst : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add per-lijst pricing fields (nullable = use global default when null)
            migrationBuilder.AddColumn<decimal>(
                name: "WinstFactor",
                table: "TypeLijsten",
                type: "TEXT",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AfvalPercentage",
                table: "TypeLijsten",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            // Migrate old staaflijst data: give those lists explicit values matching the old global defaults
            migrationBuilder.Sql(
                "UPDATE TypeLijsten SET WinstFactor = 3.5, AfvalPercentage = 20 WHERE IsStaaflijst = 1;");

            // Drop the IsStaaflijst flag – no longer needed
            migrationBuilder.DropColumn(
                name: "IsStaaflijst",
                table: "TypeLijsten");

            // Remove the staaflijst-specific global settings from Instellingen
            migrationBuilder.Sql(
                "DELETE FROM Instellingen WHERE Sleutel IN ('StaaflijstWinstFactor','StaaflijstAfvalPercentage');");
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
