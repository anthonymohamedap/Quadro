using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteAfwerkingen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "AfwerkingsOpties",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGearchiveerd",
                table: "AfwerkingsVarianten",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "AfwerkingsOpties");

            migrationBuilder.DropColumn(
                name: "IsGearchiveerd",
                table: "AfwerkingsVarianten");
        }
    }
}
