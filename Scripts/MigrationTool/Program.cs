/*
 * QuadroApp — SQLite → PostgreSQL migratie tool
 * ================================================
 * Vereisten: .NET 9 (al geïnstalleerd op Anthony's laptop)
 *
 * Gebruik:
 *   dotnet run --project Scripts/MigrationTool -- ^
 *     --sqlite "C:\pad\naar\quadro.db" ^
 *     --pg "Host=192.168.1.X;Port=5432;Database=quadrodb;Username=quadro;Password=WACHTWOORD"
 *
 * Op Mac/Linux: gebruik \ vervangen door / en ^ door \
 */

using Microsoft.Data.Sqlite;
using Npgsql;

// ── Tabel-volgorde (FK-safe: ouders voor kinderen) ───────────────────────────
var tables = new[]
{
    "Leveranciers",
    "AfwerkingsGroepen",
    "Klanten",
    "ImportSessions",
    "Instellingen",
    "TypeLijsten",
    "AfwerkingsOpties",
    "Offertes",
    "WerkBonnen",
    "WerkTaken",
    "OfferteRegels",
    "ImportRowLogs",
    "LeverancierBestellingen",
    "LeverancierBestelLijnen",
    "VoorraadMutaties",
    "VoorraadAlerts",
    "Facturen",
    "FactuurLijnen",
    "GeblokkeerDagen",
    "WerkBonArchieven",
    "OfferteArchieven",
};

// Tabellen zonder een integer Id-kolom (geen sequence om te resetten)
var noSequence = new HashSet<string> { "Instellingen" };

// ── Argumenten inlezen ───────────────────────────────────────────────────────
string sqlitePath = "";
string pgConnStr  = "";

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--sqlite") sqlitePath = args[i + 1];
    if (args[i] == "--pg")     pgConnStr  = args[i + 1];
}

if (string.IsNullOrWhiteSpace(sqlitePath) || string.IsNullOrWhiteSpace(pgConnStr))
{
    Console.WriteLine("Gebruik:");
    Console.WriteLine("  dotnet run --project Scripts/MigrationTool -- --sqlite \"pad/naar/quadro.db\" --pg \"Host=...\"");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine("============================================================");
Console.WriteLine("  QuadroApp — SQLite → PostgreSQL migratie");
Console.WriteLine("============================================================");
Console.WriteLine($"  SQLite: {sqlitePath}");
Console.WriteLine();

// ── Verbindingen openen ──────────────────────────────────────────────────────
await using var sqConn = new SqliteConnection($"Data Source={sqlitePath};Mode=ReadOnly");
await sqConn.OpenAsync();
Console.WriteLine("✅ SQLite verbonden");

await using var pgConn = new NpgsqlConnection(pgConnStr);
await pgConn.OpenAsync();
Console.WriteLine("✅ PostgreSQL verbonden");
Console.WriteLine();

// ── Welke tabellen bestaan in SQLite? ────────────────────────────────────────
var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await using (var cmd = sqConn.CreateCommand())
{
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
        existingTables.Add(r.GetString(0));
}

// ── FK-checks uitschakelen in PostgreSQL ─────────────────────────────────────
await using (var cmd = pgConn.CreateCommand())
{
    cmd.CommandText = "SET session_replication_role = replica;";
    await cmd.ExecuteNonQueryAsync();
}

int totalRows = 0;
var failedTables = new List<string>();

foreach (var table in tables)
{
    if (!existingTables.Contains(table))
    {
        Console.WriteLine($"  ⚠️  {table,-35} niet gevonden in SQLite, overgeslagen");
        continue;
    }

    try
    {
        // Kolomnamen ophalen uit SQLite
        var columns = new List<string>();
        await using (var cmd = sqConn.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                columns.Add(r.GetString(1)); // kolom 1 = naam
        }

        if (columns.Count == 0) continue;

        var colList     = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var paramList   = string.Join(", ", columns.Select((_, i) => $"${i + 1}"));
        var insertSql   = $"INSERT INTO \"{table}\" ({colList}) VALUES ({paramList}) ON CONFLICT DO NOTHING";

        // Alle rijen uit SQLite lezen
        var allRows = new List<object?[]>();
        await using (var cmd = sqConn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {colList} FROM \"{table}\"";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new object?[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                    row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
                allRows.Add(row);
            }
        }

        if (allRows.Count == 0)
        {
            Console.WriteLine($"  ✅ {table,-35}      0 rijen (leeg)");
            continue;
        }

        // Naar PostgreSQL schrijven
        await using var transaction = await pgConn.BeginTransactionAsync();
        foreach (var row in allRows)
        {
            await using var cmd = pgConn.CreateCommand();
            cmd.CommandText = insertSql;
            cmd.Transaction = (NpgsqlTransaction)transaction;
            for (int i = 0; i < row.Length; i++)
                cmd.Parameters.AddWithValue($"${i + 1}", row[i] ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();

        totalRows += allRows.Count;
        Console.WriteLine($"  ✅ {table,-35} {allRows.Count,6} rijen");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ❌ {table,-35} FOUT: {ex.Message}");
        failedTables.Add(table);
    }
}

// ── FK-checks weer inschakelen ───────────────────────────────────────────────
await using (var cmd = pgConn.CreateCommand())
{
    cmd.CommandText = "SET session_replication_role = DEFAULT;";
    await cmd.ExecuteNonQueryAsync();
}

if (failedTables.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"⚠️  {failedTables.Count} tabel(len) mislukt: {string.Join(", ", failedTables)}");
    Environment.Exit(1);
}

// ── Sequences resetten ───────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("  Sequenties resetten...");
foreach (var table in tables)
{
    if (noSequence.Contains(table) || !existingTables.Contains(table)) continue;
    try
    {
        await using var cmd = pgConn.CreateCommand();
        cmd.CommandText = $"""
            SELECT setval(
                pg_get_serial_sequence('"{table}"', 'Id'),
                COALESCE((SELECT MAX("Id") FROM "{table}"), 0) + 1,
                false
            )
            """;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  🔢 {table,-35} sequence gereset");
    }
    catch
    {
        // Tabel heeft geen integer Id-sequence — stilletjes overslaan
    }
}

Console.WriteLine();
Console.WriteLine("============================================================");
Console.WriteLine($"  ✅ Migratie klaar — {totalRows} rijen gekopieerd");
Console.WriteLine("============================================================");
Console.WriteLine();
