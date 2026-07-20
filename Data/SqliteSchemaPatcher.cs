using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Data;

/// <summary>
/// US-30 — database initialisation for SQLite.
///
/// Two paths:
///   1. FRESH database (no tables): a clean <c>MigrateAsync()</c> creates the
///      entire schema from the Baseline migration, including migration history.
///   2. EXISTING database (created via EnsureCreated + years of raw patches):
///      idempotent "healer" patches bring the schema to Baseline shape, the
///      Baseline migration is then marked as applied, and stale (pre-squash)
///      history entries are removed. Future migrations run normally via
///      <c>MigrateAsync()</c>.
///
/// The healer patch list is FROZEN: it describes the diff between the oldest
/// database in the field and the Baseline. New schema changes must be added as
/// regular EF migrations, never here.
/// </summary>
public static class SqliteSchemaPatcher
{
    public static async Task PatchAsync(AppDbContext db, ILogger logger)
    {
        var conn = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // ── Fresh database? → clean migrate, done. ───────────────────────────
        if (!await TableExistsAsync(conn, "Klanten"))
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("[DB] Verse database aangemaakt via migraties (Baseline).");
            return;
        }

        // ── Existing database → heal to Baseline shape (idempotent) ─────────
        await ApplyLegacyHealerPatchesAsync(db, conn, logger);

        // ── Mark Baseline (and any defined migration whose schema-changes the
        //    healer already guarantees) as applied; drop stale pre-squash rows. ─
        await SyncMigrationHistoryAsync(db, logger);

        // ── Run any genuinely new migrations ─────────────────────────────────
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// FROZEN legacy patch set — heals any pre-Baseline database (originally
    /// created via EnsureCreated) to the exact Baseline schema shape.
    /// Do not add new patches here; write EF migrations instead.
    /// </summary>
    private static async Task ApplyLegacyHealerPatchesAsync(
        AppDbContext db, Microsoft.Data.Sqlite.SqliteConnection conn, ILogger logger)
    {
#pragma warning disable EF1002  // Raw SQL with no user input — safe
        // Archief-tabellen
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"WerkBonArchieven\" (" +
                "\"Id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"OrigineleWerkBonId\" INTEGER NOT NULL," +
                "\"OfferteId\" INTEGER NOT NULL," +
                "\"KlantNaam\" TEXT NOT NULL," +
                "\"KlantId\" INTEGER NULL," +
                "\"OfferteDatum\" TEXT NOT NULL," +
                "\"OfferteStatusOpMoment\" TEXT NOT NULL," +
                "\"WerkBonStatusOpMoment\" TEXT NOT NULL," +
                "\"TotaalPrijsIncl\" TEXT NOT NULL," +
                "\"GearchiveerdOp\" TEXT NOT NULL," +
                "\"AnnuleringsReden\" TEXT NULL," +
                "\"Snapshot\" TEXT NOT NULL," +
                "\"IsHersteld\" INTEGER NOT NULL," +
                "\"HersteldNaarOfferteId\" INTEGER NULL" +
                ")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_GearchiveerdOp""     ON ""WerkBonArchieven""(""GearchiveerdOp"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OrigineleWerkBonId"" ON ""WerkBonArchieven""(""OrigineleWerkBonId"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OfferteId""          ON ""WerkBonArchieven""(""OfferteId"")");

            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"OfferteArchieven\" (" +
                "\"Id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"OrigineleOfferteId\" INTEGER NOT NULL," +
                "\"KlantNaam\" TEXT NOT NULL," +
                "\"KlantId\" INTEGER NULL," +
                "\"OfferteDatum\" TEXT NOT NULL," +
                "\"Jaar\" INTEGER NOT NULL," +
                "\"StatusOpMoment\" TEXT NOT NULL," +
                "\"TotaalInclBtw\" TEXT NOT NULL," +
                "\"HadWerkBon\" INTEGER NOT NULL," +
                "\"GearchiveerdOp\" TEXT NOT NULL," +
                "\"Reden\" TEXT NULL," +
                "\"Snapshot\" TEXT NOT NULL," +
                "\"IsHersteld\" INTEGER NOT NULL," +
                "\"HersteldNaarOfferteId\" INTEGER NULL" +
                ")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_GearchiveerdOp""     ON ""OfferteArchieven""(""GearchiveerdOp"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_Jaar""               ON ""OfferteArchieven""(""Jaar"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_OrigineleOfferteId"" ON ""OfferteArchieven""(""OrigineleOfferteId"")");

            logger.LogInformation("[DB] Archief-tabellen gecontroleerd/aangemaakt.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DB] FOUT bij aanmaken archief-tabellen: {Message}", ex.Message);
        }

        // Kolom-patches voor oudere databases
        if (!await ColumnExistsAsync(conn, "AfwerkingsOpties", "Kleur"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AfwerkingsOpties\" ADD COLUMN \"Kleur\" TEXT NOT NULL DEFAULT 'Standaard'");

        try { await db.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer\""); }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase))
        { /* index bestond al niet — prima */ }
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur\" " +
            "ON \"AfwerkingsOpties\" (\"AfwerkingsGroepId\", \"Volgnummer\", \"Kleur\")");

        if (!await ColumnExistsAsync(conn, "OfferteRegels", "Titel"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"Titel\" TEXT NULL");

        if (!await ColumnExistsAsync(conn, "Offertes", "RowVersion"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"RowVersion\" BLOB NULL");

        if (!await ColumnExistsAsync(conn, "Facturen", "GeplandeDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"GeplandeDatum\" TEXT NULL");

        if (!await ColumnExistsAsync(conn, "Facturen", "KortingPct"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"KortingPct\" TEXT NOT NULL DEFAULT '0'");
        if (!await ColumnExistsAsync(conn, "Facturen", "KortingBedragExcl"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"KortingBedragExcl\" TEXT NOT NULL DEFAULT '0'");

        // AfwerkingsVarianten
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""AfwerkingsVarianten"" (
    ""Id""                  INTEGER NOT NULL CONSTRAINT ""PK_AfwerkingsVarianten"" PRIMARY KEY AUTOINCREMENT,
    ""AfwerkingsOptieId""   INTEGER NOT NULL,
    ""Beschrijving""        TEXT    NOT NULL,
    ""Kleur""               TEXT    NULL,
    ""VariantCode""         TEXT    NULL,
    ""IsStandaard""         INTEGER NOT NULL DEFAULT 0,
    ""IsActief""            INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT ""FK_AfwerkingsVarianten_AfwerkingsOpties_AfwerkingsOptieId""
        FOREIGN KEY (""AfwerkingsOptieId"") REFERENCES ""AfwerkingsOpties"" (""Id"") ON DELETE CASCADE
);");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AfwerkingsVarianten_OptieId_Beschrijving""
    ON ""AfwerkingsVarianten"" (""AfwerkingsOptieId"", ""Beschrijving"");");
        await db.Database.ExecuteSqlRawAsync(@"
INSERT OR IGNORE INTO ""AfwerkingsVarianten"" (""AfwerkingsOptieId"", ""Beschrijving"", ""IsStandaard"", ""IsActief"")
SELECT ""Id"", COALESCE(NULLIF(TRIM(""Kleur""), ''), 'Standaard'), 1, 1
FROM ""AfwerkingsOpties""
WHERE NOT EXISTS (
    SELECT 1 FROM ""AfwerkingsVarianten"" v WHERE v.""AfwerkingsOptieId"" = ""AfwerkingsOpties"".""Id""
);");

        if (!await ColumnExistsAsync(conn, "Offertes", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");
        if (!await ColumnExistsAsync(conn, "Facturen", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");
        if (!await ColumnExistsAsync(conn, "OfferteRegels", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");

        foreach (var kol in new[]
                 {
                     "GlasVariantId", "PassePartout1VariantId", "PassePartout2VariantId",
                     "DiepteKernVariantId", "OpklevenVariantId", "RugVariantId"
                 })
        {
            if (!await ColumnExistsAsync(conn, "OfferteRegels", kol))
                await db.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE \"OfferteRegels\" ADD COLUMN \"{kol}\" INTEGER NULL");
        }

        if (!await ColumnExistsAsync(conn, "LeverancierBestelLijnen", "BestelVorm"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"LeverancierBestelLijnen\" ADD COLUMN \"BestelVorm\" INTEGER NOT NULL DEFAULT 0");

        // Soft-delete kolommen
        if (!await ColumnExistsAsync(conn, "Klanten", "IsGearchiveerd"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Klanten\" ADD COLUMN \"IsGearchiveerd\" INTEGER NOT NULL DEFAULT 0");
        if (!await ColumnExistsAsync(conn, "Klanten", "GearchiveerdOp"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Klanten\" ADD COLUMN \"GearchiveerdOp\" TEXT NULL");

        if (!await ColumnExistsAsync(conn, "TypeLijsten", "IsGearchiveerd"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"TypeLijsten\" ADD COLUMN \"IsGearchiveerd\" INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync(conn, "Leveranciers", "IsGearchiveerd"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Leveranciers\" ADD COLUMN \"IsGearchiveerd\" INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync(conn, "AfwerkingsOpties", "IsGearchiveerd"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AfwerkingsOpties\" ADD COLUMN \"IsGearchiveerd\" INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync(conn, "AfwerkingsVarianten", "IsGearchiveerd"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AfwerkingsVarianten\" ADD COLUMN \"IsGearchiveerd\" INTEGER NOT NULL DEFAULT 0");

        // LeverancierBestellingen
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""LeverancierBestellingen"" (
    ""Id""               INTEGER NOT NULL CONSTRAINT ""PK_LeverancierBestellingen"" PRIMARY KEY AUTOINCREMENT,
    ""AangemaaktDoor""   TEXT    NULL,
    ""BestelNummer""     TEXT    NOT NULL,
    ""BesteldOp""        TEXT    NOT NULL,
    ""LeverancierId""    INTEGER NULL,
    ""OntvangenOp""      TEXT    NULL,
    ""Opmerking""        TEXT    NULL,
    ""Status""           TEXT    NOT NULL,
    ""VerwachteLeverdatum"" TEXT NULL,
    CONSTRAINT ""FK_LeverancierBestellingen_Leveranciers_LeverancierId""
        FOREIGN KEY (""LeverancierId"") REFERENCES ""Leveranciers"" (""Id"") ON DELETE SET NULL
);");
        await db.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LeverancierBestellingen_BestelNummer"" ON ""LeverancierBestellingen"" (""BestelNummer"")");
        await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_LeverancierBestellingen_LeverancierId"" ON ""LeverancierBestellingen"" (""LeverancierId"")");

        // VoorraadAlerts
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""VoorraadAlerts"" (
    ""Id""                    INTEGER NOT NULL CONSTRAINT ""PK_VoorraadAlerts"" PRIMARY KEY AUTOINCREMENT,
    ""TypeLijstId""           INTEGER NULL,
    ""AlertType""             TEXT    NOT NULL,
    ""Status""                TEXT    NOT NULL,
    ""AangemaaktOp""          TEXT    NOT NULL,
    ""LaatstHerinnerdOp""     TEXT    NULL,
    ""VolgendeHerinneringOp"" TEXT    NULL,
    ""BronReferentie""        TEXT    NULL,
    ""Bericht""               TEXT    NOT NULL,
    CONSTRAINT ""FK_VoorraadAlerts_TypeLijsten_TypeLijstId""
        FOREIGN KEY (""TypeLijstId"") REFERENCES ""TypeLijsten"" (""Id"") ON DELETE SET NULL
)");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_VoorraadAlerts_TypeLijstId"" ON ""VoorraadAlerts"" (""TypeLijstId"")");

        // Factuur-schema patches (voorheen FactuurSchemaUpgrade)
        await FactuurSchemaUpgrade.EnsureAsync(db);
#pragma warning restore EF1002
    }

    /// <summary>
    /// Aligns __EFMigrationsHistory with the migrations defined in the assembly:
    /// removes stale pre-squash entries and marks all defined migrations
    /// (i.e. Baseline) as applied — the healer just guaranteed the schema shape.
    /// </summary>
    private static async Task SyncMigrationHistoryAsync(AppDbContext db, ILogger logger)
    {
        const string historyTable = "__EFMigrationsHistory";
        var defined = db.Database.GetMigrations().ToList();
        if (defined.Count == 0)
        {
            logger.LogWarning("[Migration] Geen migraties gevonden in de assembly — Baseline ontbreekt?");
            return;
        }

#pragma warning disable EF1002 // migration ids come from compiled code, not user input
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE IF NOT EXISTS \"{historyTable}\" " +
            "(\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
            "\"ProductVersion\" TEXT NOT NULL)");

        var inList = string.Join(", ", defined.Select(m => $"'{m}'"));
        var removed = await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM \"{historyTable}\" WHERE \"MigrationId\" NOT IN ({inList})");
        if (removed > 0)
            logger.LogInformation("[Migration] {Count} verouderde (pre-squash) history-rijen verwijderd.", removed);

        foreach (var m in defined)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"INSERT OR IGNORE INTO \"{historyTable}\" VALUES ('{m}', '10.0.0')");
        }
#pragma warning restore EF1002
    }

    private static async Task<bool> TableExistsAsync(
        Microsoft.Data.Sqlite.SqliteConnection conn, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = table;
        cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync() is not null and not System.DBNull;
    }

    private static async Task<bool> ColumnExistsAsync(
        Microsoft.Data.Sqlite.SqliteConnection conn, string table, string column)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name=$column";
        var pt = cmd.CreateParameter(); pt.ParameterName = "$table"; pt.Value = table; cmd.Parameters.Add(pt);
        var pc = cmd.CreateParameter(); pc.ParameterName = "$column"; pc.Value = column; cmd.Parameters.Add(pc);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }
}
