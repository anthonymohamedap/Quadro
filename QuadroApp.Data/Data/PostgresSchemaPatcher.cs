using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Data;

/// <summary>
/// US-41 — database-initialisatie voor PostgreSQL via echte EF-migraties.
///
/// Twee paden (spiegelt <see cref="SqliteSchemaPatcher"/>, maar simpeler):
///   1. VERSE database (geen tabellen): <c>MigrateAsync()</c> bouwt het hele schema
///      uit de Baseline-migratie, inclusief migratiehistorie.
///   2. BESTAANDE database (ooit via <c>EnsureCreatedAsync</c> aangemaakt, mét tabellen
///      maar ZONDER <c>__EFMigrationsHistory</c>): de Baseline wordt als "al toegepast"
///      gemarkeerd (enkel een history-rij, géén DDL) en daarna draait <c>MigrateAsync()</c>
///      enkel de eventueel nieuwere migraties.
///
/// Er zijn GEEN legacy-healer-patches nodig: de PG-DB is via EnsureCreated uit exact
/// hetzelfde EF-model gebouwd en heeft dus al de Baseline-vorm. Nieuwe schemawijzigingen
/// komen voortaan als gewone EF-migraties in QuadroApp.Migrations.Npgsql.
/// </summary>
public static class PostgresSchemaPatcher
{
    public static async Task PatchAsync(AppDbContext db, ILogger logger)
    {
        // ── Verse database? → schone migratie, klaar. ────────────────────────
        if (!await TableExistsAsync(db, "Klanten"))
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("[DB] Verse PostgreSQL-database aangemaakt via migraties (Baseline).");
            return;
        }

        // ── Bestaande (EnsureCreated-)database → Baseline markeren, dan migreren. ─
        await MarkBaselineAppliedAsync(db, logger);
        await db.Database.MigrateAsync();
        logger.LogInformation("[DB] PostgreSQL-migraties toegepast (bestaande DB, Baseline gemarkeerd).");
    }

    /// <summary>
    /// Registreert de Baseline-migratie als "al toegepast" in <c>__EFMigrationsHistory</c>
    /// zonder de DDL te draaien. Idempotent: op een DB die al historie heeft (bv. verse
    /// install via MigrateAsync, of een tweede opstart) verandert er niets.
    /// </summary>
    private static async Task MarkBaselineAppliedAsync(AppDbContext db, ILogger logger)
    {
        var baseline = db.Database.GetMigrations()
            .FirstOrDefault(m => m.EndsWith("_Baseline", StringComparison.Ordinal));
        if (baseline is null)
        {
            logger.LogWarning("[Migration] Baseline-migratie niet gevonden in de assembly.");
            return;
        }

#pragma warning disable EF1002 // migratie-id komt uit gecompileerde code, geen user-input
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
            "\"MigrationId\" character varying(150) NOT NULL " +
            "CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
            "\"ProductVersion\" character varying(32) NOT NULL)");

        var rows = await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
            $"VALUES ('{baseline}', '9.0.9') ON CONFLICT (\"MigrationId\") DO NOTHING");
#pragma warning restore EF1002

        if (rows > 0)
            logger.LogInformation("[Migration] Baseline '{Baseline}' gemarkeerd als toegepast op de bestaande DB.", baseline);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = @t LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "@t";
        p.Value = table;
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }
}
