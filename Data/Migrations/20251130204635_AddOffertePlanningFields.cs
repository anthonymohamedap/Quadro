using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOffertePlanningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Offertes_Klanten_KlantId",
                table: "Offertes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WerkTaak_Duur_Positive",
                table: "WerkTaken");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WerkTaak_Tot_After_Van",
                table: "WerkTaken");

            migrationBuilder.DropIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WerkBonnen",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineDatum",
                table: "Offertes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeplandeDatum",
                table: "Offertes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeschatteMinuten",
                table: "Offertes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Offertes",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen",
                column: "OfferteId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Offertes_Klanten_KlantId",
                table: "Offertes",
                column: "KlantId",
                principalTable: "Klanten",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Offertes_Klanten_KlantId",
                table: "Offertes");

            migrationBuilder.DropIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen");

            migrationBuilder.DropColumn(
                name: "DeadlineDatum",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "GeplandeDatum",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "GeschatteMinuten",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Offertes");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "WerkBonnen",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 30);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WerkTaak_Duur_Positive",
                table: "WerkTaken",
                sql: "[DuurMinuten] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WerkTaak_Tot_After_Van",
                table: "WerkTaken",
                sql: "[GeplandTot] > [GeplandVan]");

            migrationBuilder.CreateIndex(
                name: "IX_WerkBonnen_OfferteId",
                table: "WerkBonnen",
                column: "OfferteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Offertes_Klanten_KlantId",
                table: "Offertes",
                column: "KlantId",
                principalTable: "Klanten",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
