using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class OffertePrijsUitbreiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoorschotBetaald",
                table: "Offertes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "KortingPct",
                table: "Offertes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MeerPrijsIncl",
                table: "Offertes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VoorschotBedrag",
                table: "Offertes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Opmerking",
                table: "OfferteRegels",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoorschotBetaald",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "KortingPct",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "MeerPrijsIncl",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "VoorschotBedrag",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "Opmerking",
                table: "OfferteRegels");
        }
    }
}
