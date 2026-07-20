using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Data;

/// <summary>
/// Applies idempotent SQLite schema patches and EF Core migration history seeding
/// for databases that were originally created via EnsureCreatedAsync.
/// Called once at startup from App.axaml.cs → InitializeSqliteDatabaseAsync.
/// </summary>
internal static class SqliteSchemaPatcher
{
    /// <summary>
    /// Applies EF Core migrations safely, even on a DB that was originally
    /// created via EnsureCreatedAsync (which has no __EFMigrationsHistory table).
    /// Marks all pre-existing migrations as applied, then runs only new ones.
    /// </summary>
    public static async Task PatchAsync(AppDbContext db, ILogger logger)
    {
        const string historyTable = "__EFMigrationsHistory";

        // ── Stap 1: archief-tabellen ALTIJD aanmaken via raw SQL ─────────────
        // Dit staat los van het migratie-systeem zodat een vroeg-falende catch
        // de tabelcreatie niet kan overslaan.
#pragma warning disable EF1002  // Raw SQL with no user input — safe
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

        // ── Stap 1b: Schema-patches voor kolommen die mogelijk ontbreken op oudere DBs ─
        // PRAGMA table_info check first: no ALTER TABLE attempted when column already exists,
        // so EF Core never logs a scary "fail:" line for a harmless duplicate-column error.

        var conn = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // AddAfwerkingsKleur (20260323120500) — Kleur op AfwerkingsOpties
        if (!await ColumnExistsAsync(conn, "AfwerkingsOpties", "Kleur"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AfwerkingsOpties\" ADD COLUMN \"Kleur\" TEXT NOT NULL DEFAULT 'Standaard'");

        // AddAfwerkingsKleur (20260323120500) — uniek index updaten naar versie mét Kleur
        // SQLite error 1 = SQLITE_ERROR; "no such index" is the only expected failure for DROP.
        try { await db.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer\""); }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase))
        { /* index bestond al niet — prima */ }
        // IF NOT EXISTS makes the CREATE idempotent; no catch needed.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur\" " +
            "ON \"AfwerkingsOpties\" (\"AfwerkingsGroepId\", \"Volgnummer\", \"Kleur\")");

        // AddTitelToOfferteRegel (20260321121423) — Titel op OfferteRegels
        if (!await ColumnExistsAsync(conn, "OfferteRegels", "Titel"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"Titel\" TEXT NULL");

        // AddRowVersionToOfferte (20260506000000) — optimistic concurrency token
        if (!await ColumnExistsAsync(conn, "Offertes", "RowVersion"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"RowVersion\" BLOB NULL");

        // AddGeplandeDatumToFactuur (20260506130000)
        if (!await ColumnExistsAsync(conn, "Facturen", "GeplandeDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"GeplandeDatum\" TEXT NULL");

        // AddKortingToFactuur (US-23) — korting expliciet op de bestelbon
        if (!await ColumnExistsAsync(conn, "Facturen", "KortingPct"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"KortingPct\" TEXT NOT NULL DEFAULT '0'");
        if (!await ColumnExistsAsync(conn, "Facturen", "KortingBedragExcl"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"KortingBedragExcl\" TEXT NOT NULL DEFAULT '0'");

        // AddAfwerkingsVariant (20260507110000)
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
        // Auto-migrate: maak 1 variant per bestaande optie als nog niet aangemaakt
        await db.Database.ExecuteSqlRawAsync(@"
INSERT OR IGNORE INTO ""AfwerkingsVarianten"" (""AfwerkingsOptieId"", ""Beschrijving"", ""IsStandaard"", ""IsActief"")
SELECT ""Id"", COALESCE(NULLIF(TRIM(""Kleur""), ''), 'Standaard'), 1, 1
FROM ""AfwerkingsOpties""
WHERE NOT EXISTS (
    SELECT 1 FROM ""AfwerkingsVarianten"" v WHERE v.""AfwerkingsOptieId"" = ""AfwerkingsOpties"".""Id""
);");

        // AddAfhaalDatum (20260507100000)
        if (!await ColumnExistsAsync(conn, "Offertes", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");
        if (!await ColumnExistsAsync(conn, "Facturen", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");

        // AddAfhaalDatumToOfferteRegel (20260507130000)
        if (!await ColumnExistsAsync(conn, "OfferteRegels", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");

        // AddAfwerkingVariantenToOfferteRegel — gekozen variant per afwerking-slot (nullable FK)
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

        // AddBestelVormToBestellijn (20260507093626)
        if (!await ColumnExistsAsync(conn, "LeverancierBestelLijnen", "BestelVorm"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"LeverancierBestelLijnen\" ADD COLUMN \"BestelVorm\" INTEGER NOT NULL DEFAULT 0");

        // Soft-delete (20260520000001..4) — IsGearchiveerd/GearchiveerdOp.
        // De bijbehorende migraties missen hun [Migration]/Designer-bestanden en worden
        // daardoor NIET door MigrateAsync herkend; de globale query-filters
        // (!IsGearchiveerd) op Klant/TypeLijst/Leverancier/AfwerkingsOptie crashen
        // zonder deze kolommen. Daarom hier als idempotente raw-patch.
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

        // LeverancierBestellingen — never in an EF-recognised migration; always created by
        // EnsureCreatedAsync for new DBs, but older DBs may be missing it.
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

        // AddVoorraadAlerts (20260506120000) — ensure table exists before HomeViewModel loads
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

        // ── Stap 1c: Factuur-schema patches (voorheen FactuurSchemaUpgrade, ──
        //    aangeroepen op 11 plekken per factuur-operatie; nu eenmalig hier).
        await FactuurSchemaUpgrade.EnsureAsync(db);

        // ── Stap 2: EF migratie-historietabel + pre-existing migrations ──────
        var preExistingMigrations = new[]
        {
            "20260228232856_InitialClean",
            "20260303090000_AddStaaflijstSettingsAndFlag",
            "20260303091000_RemoveTypeLijstMarginColumns",
            "20260303141137_fixes",
            "20260318000000_AddTypeLijstPerLijstPricingDropStaaflijst",
            "20260321121423_AddTitelToOfferteRegel",
            "20260323120500_AddAfwerkingsKleur",
            "20260407000000_NullableLeverancierIdOnTypeLijst",
            // "20260408161449_home" — removed: no matching .cs migration file exists,
            // would crash MigrateAsync on an existing DB that doesn't have it applied.
            "20260408120000_AddWerkBonArchief",
            "20260408140000_AddOfferteArchief",
            "20260506000000_AddRowVersionToOfferte",
            "20260506120000_AddVoorraadAlerts",
            "20260506130000_AddGeplandeDatumToFactuur",
            "20260507100000_AddAfhaalDatum",
            "20260507110000_AddAfwerkingsVariant",
            "20260507011133_tag10",
            "20260507023057_date",
            "20260507130000_AddAfhaalDatumToOfferteRegel",
            "20260507093626_AddBestelVormToBestellijn",
            // Soft-delete: kolommen worden via raw-patch hierboven aangemaakt; markeer
            // de (Designer-loze) migraties als toegepast zodat MigrateAsync ze niet probeert.
            "20260520000001_AddSoftDeleteKlant",
            "20260520000002_AddSoftDeleteTypeLijst",
            "20260520000003_AddSoftDeleteAfwerkingen",
            "20260520000004_AddSoftDeleteLeverancier",
            // SyncPendingModelChanges adds columns already applied by raw patches above and
            // rebuilds LeverancierBestellingen (also handled above).  Mark as applied so
            // MigrateAsync does not try to run it against a DB with existing columns.
            "20260617233009_SyncPendingModelChanges",
        };

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE TABLE IF NOT EXISTS \"{historyTable}\" " +
                "(\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL)");

            foreach (var m in preExistingMigrations)
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"INSERT OR IGNORE INTO \"{historyTable}\" VALUES ('{m}', '9.0.0')");
            }

            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Migration] Warning: {Message}", ex.Message);
        }
#pragma warning restore EF1002
    }

    private static async Task<bool> ColumnExistsAsync(
        Microsoft.Data.Sqlite.SqliteConnection conn, string table, string column)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }
}
