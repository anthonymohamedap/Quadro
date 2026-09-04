using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyKantKlaarKader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreedteCm",
                table: "KantKlaarKaders");

            migrationBuilder.DropColumn(
                name: "HoogteCm",
                table: "KantKlaarKaders");

            migrationBuilder.AddColumn<string>(
                name: "Beschrijving",
                table: "KantKlaarKaders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Beschrijving",
                table: "KantKlaarKaders");

            migrationBuilder.AddColumn<decimal>(
                name: "BreedteCm",
                table: "KantKlaarKaders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HoogteCm",
                table: "KantKlaarKaders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
