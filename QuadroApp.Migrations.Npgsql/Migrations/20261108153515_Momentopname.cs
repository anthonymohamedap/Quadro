using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class Momentopname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AfgerondOp",
                table: "Offertes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Momentopname",
                table: "Offertes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfgerondOp",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "Momentopname",
                table: "Offertes");
        }
    }
}
