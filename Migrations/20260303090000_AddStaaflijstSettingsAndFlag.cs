using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class AddStaaflijstSettingsAndFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStaaflijst",
                table: "TypeLijsten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE TypeLijsten SET IsStaaflijst = 1 WHERE UPPER(COALESCE(Soort,'')) IN ('HOU','HOUT');");

            migrationBuilder.Sql("INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'StaaflijstWinstFactor', '3.5' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'StaaflijstWinstFactor');");
            migrationBuilder.Sql("INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'StaaflijstAfvalPercentage', '20' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'StaaflijstAfvalPercentage');");
            migrationBuilder.Sql("INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'DefaultWinstFactor', '0' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'DefaultWinstFactor');");
            migrationBuilder.Sql("INSERT INTO Instellingen (Sleutel, Waarde) SELECT 'DefaultAfvalPercentage', '0' WHERE NOT EXISTS (SELECT 1 FROM Instellingen WHERE Sleutel = 'DefaultAfvalPercentage');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Instellingen WHERE Sleutel IN ('StaaflijstWinstFactor','StaaflijstAfvalPercentage','DefaultWinstFactor','DefaultAfvalPercentage');");

            migrationBuilder.DropColumn(
                name: "IsStaaflijst",
                table: "TypeLijsten");
        }
    }
}
