using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuadroApp.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class HernoemFactuurNaarBestelbon : Migration
    {
        // US-99 fase C — "factuur" wordt "bestelbon" in de database.
        // Met de hand geschreven: EF genereerde DropTable/CreateTable (= alle bestelbonnen kwijt).
        // Hier enkel hernoemingen; de gegevens blijven staan (migratietest: BestelbonMigratieTests).
        // Volgorde: sleutels los → tabellen/kolommen hernoemen → indexen hernoemen → sleutels terug.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Ontvangsten_Facturen_FactuurId", table: "Ontvangsten");
            migrationBuilder.DropForeignKey(name: "FK_FactuurLijnen_Facturen_FactuurId", table: "FactuurLijnen");
            migrationBuilder.DropForeignKey(name: "FK_Facturen_Offertes_OfferteId", table: "Facturen");
            migrationBuilder.DropForeignKey(name: "FK_Facturen_WerkBonnen_WerkBonId", table: "Facturen");
            migrationBuilder.DropPrimaryKey(name: "PK_FactuurLijnen", table: "FactuurLijnen");
            migrationBuilder.DropPrimaryKey(name: "PK_Facturen", table: "Facturen");

            migrationBuilder.RenameTable(name: "Facturen", newName: "Bestelbonnen");
            migrationBuilder.RenameTable(name: "FactuurLijnen", newName: "BestelbonLijnen");

            migrationBuilder.RenameColumn(name: "FactuurNummer", table: "Bestelbonnen", newName: "BestelbonNummer");
            migrationBuilder.RenameColumn(name: "FactuurDatum", table: "Bestelbonnen", newName: "BestelbonDatum");
            migrationBuilder.RenameColumn(name: "FactuurId", table: "BestelbonLijnen", newName: "BestelbonId");
            migrationBuilder.RenameColumn(name: "FactuurId", table: "Ontvangsten", newName: "BestelbonId");

            migrationBuilder.RenameIndex(name: "IX_Facturen_FactuurNummer", table: "Bestelbonnen", newName: "IX_Bestelbonnen_BestelbonNummer");
            migrationBuilder.RenameIndex(name: "IX_Facturen_Jaar_VolgNr", table: "Bestelbonnen", newName: "IX_Bestelbonnen_Jaar_VolgNr");
            migrationBuilder.RenameIndex(name: "IX_Facturen_OfferteId", table: "Bestelbonnen", newName: "IX_Bestelbonnen_OfferteId");
            migrationBuilder.RenameIndex(name: "IX_Facturen_WerkBonId", table: "Bestelbonnen", newName: "IX_Bestelbonnen_WerkBonId");
            migrationBuilder.RenameIndex(name: "IX_FactuurLijnen_FactuurId", table: "BestelbonLijnen", newName: "IX_BestelbonLijnen_BestelbonId");
            migrationBuilder.RenameIndex(name: "IX_Ontvangsten_FactuurId", table: "Ontvangsten", newName: "IX_Ontvangsten_BestelbonId");

            migrationBuilder.AddPrimaryKey(name: "PK_Bestelbonnen", table: "Bestelbonnen", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_BestelbonLijnen", table: "BestelbonLijnen", column: "Id");
            migrationBuilder.AddForeignKey(name: "FK_Bestelbonnen_Offertes_OfferteId", table: "Bestelbonnen", column: "OfferteId",
                principalTable: "Offertes", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Bestelbonnen_WerkBonnen_WerkBonId", table: "Bestelbonnen", column: "WerkBonId",
                principalTable: "WerkBonnen", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_BestelbonLijnen_Bestelbonnen_BestelbonId", table: "BestelbonLijnen", column: "BestelbonId",
                principalTable: "Bestelbonnen", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_Ontvangsten_Bestelbonnen_BestelbonId", table: "Ontvangsten", column: "BestelbonId",
                principalTable: "Bestelbonnen", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // Oude bonnen met documenttype "Factuur" en de auditlog volgen mee (AC 4).
            migrationBuilder.Sql("UPDATE \"Bestelbonnen\" SET \"DocumentType\" = 'Bestelbon' WHERE \"DocumentType\" = 'Factuur';");
            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"EntiteitType\" = 'Bestelbon' WHERE \"EntiteitType\" = 'Factuur';");
            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"EntiteitType\" = 'BestelbonLijn' WHERE \"EntiteitType\" = 'FactuurLijn';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DocumentType 'Factuur' wordt niet teruggezet: niet meer te onderscheiden en ook niet nodig.
            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"EntiteitType\" = 'FactuurLijn' WHERE \"EntiteitType\" = 'BestelbonLijn';");
            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"EntiteitType\" = 'Factuur' WHERE \"EntiteitType\" = 'Bestelbon';");

            migrationBuilder.DropForeignKey(name: "FK_Ontvangsten_Bestelbonnen_BestelbonId", table: "Ontvangsten");
            migrationBuilder.DropForeignKey(name: "FK_BestelbonLijnen_Bestelbonnen_BestelbonId", table: "BestelbonLijnen");
            migrationBuilder.DropForeignKey(name: "FK_Bestelbonnen_Offertes_OfferteId", table: "Bestelbonnen");
            migrationBuilder.DropForeignKey(name: "FK_Bestelbonnen_WerkBonnen_WerkBonId", table: "Bestelbonnen");
            migrationBuilder.DropPrimaryKey(name: "PK_BestelbonLijnen", table: "BestelbonLijnen");
            migrationBuilder.DropPrimaryKey(name: "PK_Bestelbonnen", table: "Bestelbonnen");

            migrationBuilder.RenameIndex(name: "IX_Bestelbonnen_BestelbonNummer", table: "Bestelbonnen", newName: "IX_Facturen_FactuurNummer");
            migrationBuilder.RenameIndex(name: "IX_Bestelbonnen_Jaar_VolgNr", table: "Bestelbonnen", newName: "IX_Facturen_Jaar_VolgNr");
            migrationBuilder.RenameIndex(name: "IX_Bestelbonnen_OfferteId", table: "Bestelbonnen", newName: "IX_Facturen_OfferteId");
            migrationBuilder.RenameIndex(name: "IX_Bestelbonnen_WerkBonId", table: "Bestelbonnen", newName: "IX_Facturen_WerkBonId");
            migrationBuilder.RenameIndex(name: "IX_BestelbonLijnen_BestelbonId", table: "BestelbonLijnen", newName: "IX_FactuurLijnen_FactuurId");
            migrationBuilder.RenameIndex(name: "IX_Ontvangsten_BestelbonId", table: "Ontvangsten", newName: "IX_Ontvangsten_FactuurId");

            migrationBuilder.RenameColumn(name: "BestelbonId", table: "Ontvangsten", newName: "FactuurId");
            migrationBuilder.RenameColumn(name: "BestelbonId", table: "BestelbonLijnen", newName: "FactuurId");
            migrationBuilder.RenameColumn(name: "BestelbonDatum", table: "Bestelbonnen", newName: "FactuurDatum");
            migrationBuilder.RenameColumn(name: "BestelbonNummer", table: "Bestelbonnen", newName: "FactuurNummer");

            migrationBuilder.RenameTable(name: "BestelbonLijnen", newName: "FactuurLijnen");
            migrationBuilder.RenameTable(name: "Bestelbonnen", newName: "Facturen");

            migrationBuilder.AddPrimaryKey(name: "PK_Facturen", table: "Facturen", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_FactuurLijnen", table: "FactuurLijnen", column: "Id");
            migrationBuilder.AddForeignKey(name: "FK_Facturen_Offertes_OfferteId", table: "Facturen", column: "OfferteId",
                principalTable: "Offertes", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Facturen_WerkBonnen_WerkBonId", table: "Facturen", column: "WerkBonId",
                principalTable: "WerkBonnen", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_FactuurLijnen_Facturen_FactuurId", table: "FactuurLijnen", column: "FactuurId",
                principalTable: "Facturen", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_Ontvangsten_Facturen_FactuurId", table: "Ontvangsten", column: "FactuurId",
                principalTable: "Facturen", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        }
    }
}
