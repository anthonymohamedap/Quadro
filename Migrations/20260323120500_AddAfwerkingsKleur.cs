using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    public partial class AddAfwerkingsKleur : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer",
                table: "AfwerkingsOpties");

            migrationBuilder.AddColumn<string>(
                name: "Kleur",
                table: "AfwerkingsOpties",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Standaard");

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur",
                table: "AfwerkingsOpties",
                columns: new[] { "AfwerkingsGroepId", "Volgnummer", "Kleur" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur",
                table: "AfwerkingsOpties");

            migrationBuilder.DropColumn(
                name: "Kleur",
                table: "AfwerkingsOpties");

            migrationBuilder.CreateIndex(
                name: "IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer",
                table: "AfwerkingsOpties",
                columns: new[] { "AfwerkingsGroepId", "Volgnummer" },
                unique: true);
        }
    }
}
