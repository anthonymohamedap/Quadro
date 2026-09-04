using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
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
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KantKlaarKaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Naam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BreedteCm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HoogteCm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrijsPerStukExcl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsGearchiveerd = table.Column<bool>(type: "boolean", nullable: false)
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
