using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteLeverancier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. IsGearchiveerd toevoegen aan Leveranciers (ADD COLUMN — geen rebuild nodig).
            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "Leveranciers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // 2. LeverancierBestellingen.LeverancierId: NOT NULL + Cascade → nullable + SetNull.
            //    SQLite vereist een volledige table rebuild voor FK/nullability wijzigingen.
            //    EF Core SQLite provider handelt dit automatisch af via:
            //    CREATE new → INSERT SELECT → DROP old → RENAME new

            // Drop de oude FK constraint door de tabel te herbouwen.
            migrationBuilder.DropForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen");

            // Maak de kolom nullable (SQLite table rebuild via AlterColumn).
            migrationBuilder.AlterColumn<int>(
                name: "LeverancierId",
                table: "LeverancierBestellingen",
                type: "INTEGER",
                nullable: true,          // was: nullable: false
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // Voeg de FK opnieuw toe met SetNull delete behavior.
            migrationBuilder.AddForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen",
                column: "LeverancierId",
                principalTable: "Leveranciers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);   // was: Cascade
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Terugzetten naar NOT NULL + Cascade.
            migrationBuilder.DropForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen");

            migrationBuilder.AlterColumn<int>(
                name: "LeverancierId",
                table: "LeverancierBestellingen",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true,
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen",
                column: "LeverancierId",
                principalTable: "Leveranciers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "Leveranciers");
        }
    }
}
