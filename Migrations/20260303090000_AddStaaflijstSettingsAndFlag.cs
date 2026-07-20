using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class AddStaaflijstSettingsAndFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema already applied to database — no-op to sync migration history.
            migrationBuilder.Sql("SELECT 1;");
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
