using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace QuadroApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGeplandeDatumToFactuur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Facturen"" ADD COLUMN ""GeplandeDatum"" TEXT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support DROP COLUMN on older versions — acceptable to leave.
        }
    }
}
