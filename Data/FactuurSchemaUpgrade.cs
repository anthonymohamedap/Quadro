using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Data;

public static class FactuurSchemaUpgrade
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite())
            return;

        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            if (!await TableExistsAsync(conn, "Facturen"))
                return;

            var columns = await ReadColumnsAsync(conn, "Facturen");

            if (!columns.Contains("DocumentType", StringComparer.OrdinalIgnoreCase))
                await ExecuteNonQueryAsync(conn, "ALTER TABLE Facturen ADD COLUMN DocumentType TEXT NOT NULL DEFAULT 'Bestelbon';");

            if (!columns.Contains("AangenomenDoorInitialen", StringComparer.OrdinalIgnoreCase))
                await ExecuteNonQueryAsync(conn, "ALTER TABLE Facturen ADD COLUMN AangenomenDoorInitialen TEXT NULL;");
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection conn, string tableName)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(DbConnection conn, string tableName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}');";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1))
                set.Add(reader.GetString(1));
        }

        return set;
    }

    private static async Task ExecuteNonQueryAsync(DbConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
