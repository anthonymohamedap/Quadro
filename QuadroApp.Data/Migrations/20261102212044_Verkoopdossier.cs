using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class Verkoopdossier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnuleerReden",
                table: "Offertes",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeannuleerdOp",
                table: "Offertes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OvergeslagenStappen",
                table: "Offertes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OverslaanReden",
                table: "Offertes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnuleerReden",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "GeannuleerdOp",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "OvergeslagenStappen",
                table: "Offertes");

            migrationBuilder.DropColumn(
                name: "OverslaanReden",
                table: "Offertes");
        }
    }
}
