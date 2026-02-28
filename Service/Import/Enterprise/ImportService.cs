using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class ImportService : IImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IExcelParser _parser;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IDbContextFactory<AppDbContext> dbFactory,
        IExcelParser parser,
        ILogger<ImportService> logger)
    {
        _dbFactory = dbFactory;
        _parser = parser;
        _logger = logger;
    }

    public async Task<QuadroApp.Model.Import.ImportResult<T>> DryRunAsync<T>(Stream stream, IExcelMap<T> map, IImportValidator<T> validator, CancellationToken ct)
    {
        _logger.LogInformation("Dry run started for {EntityName}.", map.EntityName);
        var rows = new List<ImportRowResult<T>>();
        var globalIssues = new List<ImportRowIssue>();
        var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rawRows = await _parser.ReadSheetAsync(stream, map.EntityName, ct);

        foreach (var raw in rawRows)
        {
            ct.ThrowIfCancellationRequested();
            var rowNumber = int.TryParse(raw.GetValueOrDefault("__RowNumber"), out var parsedRow) ? parsedRow : 0;
            var rowResult = new ImportRowResult<T> { RowNumber = rowNumber, Parsed = map.Create() };

            foreach (var column in map.Columns)
            {
                raw.TryGetValue(column.Header, out var cellText);
                var (ok, _, error) = column.Parser(cellText);
                if (!ok)
                {
                    rowResult.Issues.Add(new ImportRowIssue
                    {
                        RowNumber = rowNumber,
                        ColumnName = column.Key,
                        Message = error ?? "Ongeldige waarde.",
                        Severity = Severity.Error,
                        RawValue = cellText
                    });

                    continue;
                }

                if (rowResult.Parsed is not null)
                {
                    map.ApplyCell(rowResult.Parsed, column.Key, cellText, rowNumber, rowResult.Issues);
                }
            }

            if (rowResult.Parsed is not null)
            {
                var key = map.GetKey(rowResult.Parsed);
                if (!string.IsNullOrWhiteSpace(key) && !duplicateKeys.Add(key))
                {
                    rowResult.Issues.Add(new ImportRowIssue
                    {
                        RowNumber = rowNumber,
                        ColumnName = "Key",
                        Message = "Dubbele rij gedetecteerd binnen hetzelfde bestand.",
                        Severity = Severity.Error,
                        RawValue = key
                    });
                }
            }

            await validator.ValidateAsync(rowResult, db, ct);
            rows.Add(rowResult);
        }

        var summary = BuildSummary(rows, 0, 0, rows.Count(r => !r.IsValid));
        _logger.LogInformation("Dry run finished for {EntityName}. Total={Total} Valid={Valid} Invalid={Invalid}", map.EntityName, summary.TotalRows, summary.ValidRows, summary.InvalidRows);

        globalIssues.Add(new ImportRowIssue { RowNumber = 0, ColumnName = "__EntityName", Message = map.EntityName, Severity = Severity.Info });

        return new QuadroApp.Model.Import.ImportResult<T>
        {
            Summary = summary,
            Rows = rows,
            GlobalIssues = globalIssues
        };
    }

    public async Task<ImportCommitReceipt> CommitAsync<T>(QuadroApp.Model.Import.ImportResult<T> preview, IImportCommitter<T> committer, CancellationToken ct)
    {
        _logger.LogInformation("Commit started for import preview.");
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await EnsureImportAuditTablesAsync(db, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var entityName = preview.GlobalIssues.FirstOrDefault(i => i.ColumnName == "__EntityName")?.Message ?? typeof(T).Name;
        var session = new ImportSession
        {
            StartedAt = DateTime.UtcNow,
            EntityName = entityName,
            FileName = "Unknown.xlsx",
            TotalRows = preview.Summary.TotalRows,
            ValidRows = preview.Summary.ValidRows,
            InvalidRows = preview.Summary.InvalidRows,
            Status = "Started"
        };

        db.Set<ImportSession>().Add(session);
        await db.SaveChangesAsync(ct);

        try
        {
            var validRows = preview.Rows.Where(r => r.IsValid).ToList();
            var (inserted, updated, skippedFromCommitter) = await committer.CommitAsync(validRows, db, ct);
            var skipped = preview.Summary.InvalidRows + skippedFromCommitter;

            session.Inserted = inserted;
            session.Updated = updated;
            session.Skipped = skipped;
            session.FinishedAt = DateTime.UtcNow;
            session.Status = "Completed";

            db.Set<ImportRowLog>().AddRange(preview.Rows.Select(r => new ImportRowLog
            {
                ImportSessionId = session.Id,
                RowNumber = r.RowNumber,
                Key = r.Parsed?.ToString() ?? string.Empty,
                Success = r.IsValid,
                Message = r.IsValid ? "Ready for commit" : "Invalid row",
                IssuesJson = JsonSerializer.Serialize(r.Issues)
            }));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Commit finished SessionId={SessionId} Inserted={Inserted} Updated={Updated} Skipped={Skipped}", session.Id, inserted, updated, skipped);

            return new ImportCommitReceipt
            {
                SessionId = session.Id,
                Inserted = inserted,
                Updated = updated,
                Skipped = skipped,
                Status = session.Status
            };
        }
        catch (OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            session.Status = "Cancelled";
            session.ErrorMessage = "Operation cancelled.";
            session.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning("Commit cancelled SessionId={SessionId}", session.Id);
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            session.Status = "Failed";
            session.ErrorMessage = ex.ToString();
            session.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "Commit failed SessionId={SessionId}", session.Id);
            throw;
        }
    }


    private static async Task EnsureImportAuditTablesAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        const string createSessions = """
CREATE TABLE IF NOT EXISTS [ImportSessions] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_ImportSessions] PRIMARY KEY AUTOINCREMENT,
    [StartedAt] TEXT NOT NULL,
    [FinishedAt] TEXT NULL,
    [EntityName] TEXT NOT NULL,
    [FileName] TEXT NOT NULL,
    [TotalRows] INTEGER NOT NULL,
    [ValidRows] INTEGER NOT NULL,
    [InvalidRows] INTEGER NOT NULL,
    [Inserted] INTEGER NOT NULL,
    [Updated] INTEGER NOT NULL,
    [Skipped] INTEGER NOT NULL,
    [Status] TEXT NOT NULL,
    [ErrorMessage] TEXT NULL
);
""";

        const string createRowLogs = """
CREATE TABLE IF NOT EXISTS [ImportRowLogs] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_ImportRowLogs] PRIMARY KEY AUTOINCREMENT,
    [ImportSessionId] INTEGER NOT NULL,
    [RowNumber] INTEGER NOT NULL,
    [Key] TEXT NOT NULL,
    [Success] INTEGER NOT NULL,
    [Message] TEXT NULL,
    [IssuesJson] TEXT NULL,
    CONSTRAINT [FK_ImportRowLogs_ImportSessions_ImportSessionId] FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions] ([Id]) ON DELETE CASCADE
);
""";

        const string createIndex = """
CREATE INDEX IF NOT EXISTS [IX_ImportRowLogs_ImportSessionId]
ON [ImportRowLogs] ([ImportSessionId]);
""";

        await db.Database.ExecuteSqlRawAsync(createSessions, ct);
        await db.Database.ExecuteSqlRawAsync(createRowLogs, ct);
        await db.Database.ExecuteSqlRawAsync(createIndex, ct);
    }

    private static ImportSummary BuildSummary<T>(IReadOnlyCollection<ImportRowResult<T>> rows, int inserts, int updates, int skipped)
    {
        return new ImportSummary
        {
            TotalRows = rows.Count,
            ValidRows = rows.Count(r => r.IsValid),
            InvalidRows = rows.Count(r => !r.IsValid),
            WarningRows = rows.Count(r => r.HasWarnings),
            InsertCount = inserts,
            UpdateCount = updates,
            SkippedCount = skipped
        };
    }
}
