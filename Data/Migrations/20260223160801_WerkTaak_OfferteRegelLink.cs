using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class WerkTaak_OfferteRegelLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OfferteRegelId",
                table: "WerkTaken",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WerkTaken_OfferteRegelId",
                table: "WerkTaken",
                column: "OfferteRegelId");

            migrationBuilder.AddForeignKey(
                name: "FK_WerkTaken_OfferteRegels_OfferteRegelId",
                table: "WerkTaken",
                column: "OfferteRegelId",
                principalTable: "OfferteRegels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WerkTaken_OfferteRegels_OfferteRegelId",
                table: "WerkTaken");

            migrationBuilder.DropIndex(
                name: "IX_WerkTaken_OfferteRegelId",
                table: "WerkTaken");

            migrationBuilder.DropColumn(
                name: "OfferteRegelId",
                table: "WerkTaken");
        }
    }
}
