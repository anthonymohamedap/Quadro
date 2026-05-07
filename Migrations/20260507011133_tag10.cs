using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class tag10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AfhaalDatum",
                table: "Offertes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Offertes",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AfhaalDatum",
                table: "Facturen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeplandeDatum",
                table: "Facturen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AfwerkingsVarianten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AfwerkingsOptieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Beschrijving = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Kleur = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    VariantCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsStandaard = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfwerkingsVarianten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AfwerkingsVarianten_AfwerkingsOpties_AfwerkingsOptieId",
                        column: x => x.AfwerkingsOptieId,
                        principalTable: "AfwerkingsOpties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsVarianten_AfwerkingsOptieId_Beschrijving",
                table: "AfwerkingsVarianten",
                columns: new[] { "AfwerkingsOptieId", "Beschrijving" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfwerkingsVarianten");

            migrationBuilder.DropColumn(
                name: "AfhaalDatum",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "AfhaalDatum",
                table: "Facturen");

            migrationBuilder.DropColumn(
                name: "GeplandeDatum",
                table: "Facturen");
        }
    }
}
