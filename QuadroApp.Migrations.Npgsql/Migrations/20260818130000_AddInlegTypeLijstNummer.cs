using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class AddInlegTypeLijstNummer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InlegTypeLijstId",
                table: "OfferteRegels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_InlegTypeLijstId",
                table: "OfferteRegels",
                column: "InlegTypeLijstId");

            migrationBuilder.AddForeignKey(
                name: "FK_OfferteRegels_TypeLijsten_InlegTypeLijstId",
                table: "OfferteRegels",
                column: "InlegTypeLijstId",
                principalTable: "TypeLijsten",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfferteRegels_TypeLijsten_InlegTypeLijstId",
                table: "OfferteRegels");

            migrationBuilder.DropIndex(
                name: "IX_OfferteRegels_InlegTypeLijstId",
                table: "OfferteRegels");

            migrationBuilder.DropColumn(
                name: "InlegTypeLijstId",
                table: "OfferteRegels");
        }
    }
}
