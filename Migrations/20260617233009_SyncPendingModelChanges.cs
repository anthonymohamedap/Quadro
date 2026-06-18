using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LeverancierBestellingen was never created by an EF-recognised migration (it was
            // always created by EnsureCreatedAsync at app startup).  On dev DBs managed by
            // `dotnet ef database update` the table therefore doesn't exist.  Create it
            // defensively before the AlterColumn table-rebuild below, which needs it to exist.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LeverancierBestellingen"" (
    ""Id""               INTEGER NOT NULL CONSTRAINT ""PK_LeverancierBestellingen"" PRIMARY KEY AUTOINCREMENT,
    ""AangemaaktDoor""   TEXT    NULL,
    ""BestelNummer""     TEXT    NOT NULL,
    ""BesteldOp""        TEXT    NOT NULL,
    ""LeverancierId""    INTEGER NULL,
    ""OntvangenOp""      TEXT    NULL,
    ""Opmerking""        TEXT    NULL,
    ""Status""           TEXT    NOT NULL,
    ""VerwachteLeverdatum"" TEXT NULL,
    CONSTRAINT ""FK_LeverancierBestellingen_Leveranciers_LeverancierId""
        FOREIGN KEY (""LeverancierId"") REFERENCES ""Leveranciers"" (""Id"") ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LeverancierBestellingen_BestelNummer""
    ON ""LeverancierBestellingen"" (""BestelNummer"");
CREATE INDEX IF NOT EXISTS ""IX_LeverancierBestellingen_LeverancierId""
    ON ""LeverancierBestellingen"" (""LeverancierId"");
");

            migrationBuilder.DropForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen");

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "TypeLijsten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "Leveranciers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "LeverancierId",
                table: "LeverancierBestellingen",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "GearchiveerdOp",
                table: "Klanten",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "Klanten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "KortingBedragExcl",
                table: "Facturen",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KortingPct",
                table: "Facturen",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "AfwerkingsVarianten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "AfwerkingsOpties",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen",
                column: "LeverancierId",
                principalTable: "Leveranciers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "TypeLijsten");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "Leveranciers");

            migrationBuilder.DropColumn(
                name: "GearchiveerdOp",
                table: "Klanten");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "Klanten");

            migrationBuilder.DropColumn(
                name: "KortingBedragExcl",
                table: "Facturen");

            migrationBuilder.DropColumn(
                name: "KortingPct",
                table: "Facturen");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "AfwerkingsVarianten");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "AfwerkingsOpties");

            migrationBuilder.AlterColumn<int>(
                name: "LeverancierId",
                table: "LeverancierBestellingen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeverancierBestellingen_Leveranciers_LeverancierId",
                table: "LeverancierBestellingen",
                column: "LeverancierId",
                principalTable: "Leveranciers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
