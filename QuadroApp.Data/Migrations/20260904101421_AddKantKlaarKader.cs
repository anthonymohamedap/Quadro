using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddKantKlaarKader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KantKlaarKaderId",
                table: "OfferteRegels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KantKlaarKaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BreedteCm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HoogteCm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PrijsPerStukExcl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KantKlaarKaders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfferteRegels_KantKlaarKaderId",
                table: "OfferteRegels",
                column: "KantKlaarKaderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OfferteRegels_KantKlaarKaders_KantKlaarKaderId",
                table: "OfferteRegels",
                column: "KantKlaarKaderId",
                principalTable: "KantKlaarKaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfferteRegels_KantKlaarKaders_KantKlaarKaderId",
                table: "OfferteRegels");

            migrationBuilder.DropTable(
                name: "KantKlaarKaders");

            migrationBuilder.DropIndex(
                name: "IX_OfferteRegels_KantKlaarKaderId",
                table: "OfferteRegels");

            migrationBuilder.DropColumn(
                name: "KantKlaarKaderId",
                table: "OfferteRegels");
        }
    }
}
