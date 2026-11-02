using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
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
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeannuleerdOp",
                table: "Offertes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OvergeslagenStappen",
                table: "Offertes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OverslaanReden",
                table: "Offertes",
                type: "character varying(200)",
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
