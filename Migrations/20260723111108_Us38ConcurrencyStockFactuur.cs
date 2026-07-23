using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class Us38ConcurrencyStockFactuur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TypeLijsten",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturen_FactuurNummer",
                table: "Facturen",
                column: "FactuurNummer",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facturen_FactuurNummer",
                table: "Facturen");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TypeLijsten");
        }
    }
}
