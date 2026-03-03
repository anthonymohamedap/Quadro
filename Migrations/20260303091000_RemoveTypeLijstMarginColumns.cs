using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class RemoveTypeLijstMarginColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WinstMargeFactor",
                table: "TypeLijsten");

            migrationBuilder.DropColumn(
                name: "AfvalPercentage",
                table: "TypeLijsten");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WinstMargeFactor",
                table: "TypeLijsten",
                type: "TEXT",
                precision: 6,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AfvalPercentage",
                table: "TypeLijsten",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
