using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public class ImportServiceTests
{
    [Fact]
    public async Task DryRun_ReturnsRowIssues_ForInvalidRows()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var sut = CreateSut(factory, new FakeParser(
            CreateRawRow(2, null),
            CreateRawRow(3, "ART-002")));

        var result = await sut.DryRunAsync(Stream.Null, TestTypeLijstMap.Instance, new NoOpValidator(), CancellationToken.None);

        Assert.Equal(2, result.Summary.TotalRows);
        Assert.Equal(1, result.Summary.ValidRows);
        Assert.Equal(1, result.Summary.InvalidRows);
        Assert.Contains(result.GlobalIssues, i => i.ColumnName == "__EntityName" && i.Message == "TypeLijst");

        var invalidRow = Assert.Single(result.Rows, r => !r.IsValid);
        Assert.Contains(invalidRow.Issues, i => i.ColumnName == "Artikelnummer");
    }

    [Fact]
    public async Task DryRun_DetectsDuplicatesInFile()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var sut = CreateSut(factory, new FakeParser(
            CreateRawRow(2, "ART-DUP"),
            CreateRawRow(3, "ART-DUP")));

        var result = await sut.DryRunAsync(Stream.Null, TestTypeLijstMap.Instance, new NoOpValidator(), CancellationToken.None);

        Assert.Equal(2, result.Summary.TotalRows);
        Assert.Equal(1, result.Summary.ValidRows);
        Assert.Equal(1, result.Summary.InvalidRows);
        Assert.Contains(result.Rows[1].Issues, i => i.Message.Contains("Dubbele rij", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DryRun_DetectsDuplicatesInDatabase()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        await SeedData.AddTypeLijstAsync(factory, "ART-EXISTING");

        var sut = CreateSut(factory, new FakeParser(CreateRawRow(2, "ART-EXISTING")));

        var result = await sut.DryRunAsync(Stream.Null, TestTypeLijstMap.Instance, new ExistingTypeLijstValidator(), CancellationToken.None);

        Assert.Equal(1, result.Summary.InvalidRows);
        var row = Assert.Single(result.Rows);
        Assert.Contains(row.Issues, i => i.Message.Contains("Bestaat al", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Commit_RollsBackOnException()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = CreateSut(sqlite.Factory);
        var preview = CreatePreview(CreateValidRow(2, "ART-ROLLBACK"));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CommitAsync(preview, new ThrowingCommitter(), CancellationToken.None));

        await using var db = await sqlite.Factory.CreateDbContextAsync();
        Assert.False(await db.Leveranciers.AnyAsync(l => l.Naam == "ERR"));
        Assert.Empty(await db.ImportRowLogs.ToListAsync());
    }

    [Fact]
    public async Task Commit_CreatesImportSessionAndRowLogs()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = CreateSut(sqlite.Factory);
        var committer = new RecordingCommitter(inserted: 1, updated: 0, skipped: 0);
        var preview = CreatePreview(
            CreateValidRow(2, "ART-001"),
            CreateInvalidRow(3, "ART-ERR", "Ongeldige rij."));

        var receipt = await sut.CommitAsync(preview, committer, CancellationToken.None);

        Assert.Equal(1, receipt.Inserted);
        Assert.Equal(0, receipt.Updated);
        Assert.Equal(1, receipt.Skipped);
        Assert.Equal("Completed", receipt.Status);
        Assert.Single(committer.ReceivedRows);
        Assert.Equal("ART-001", committer.ReceivedRows[0].Parsed?.Artikelnummer);

        await using var db = await sqlite.Factory.CreateDbContextAsync();
        var session = await db.ImportSessions.SingleAsync();
        var rowLogs = await db.ImportRowLogs.OrderBy(x => x.RowNumber).ToListAsync();

        Assert.Equal("TypeLijst", session.EntityName);
        Assert.Equal("Completed", session.Status);
        Assert.Equal(1, session.Inserted);
        Assert.Equal(1, session.Skipped);
        Assert.Equal(2, rowLogs.Count);
        Assert.Contains(rowLogs, x => x.RowNumber == 2 && x.Success);
        Assert.Contains(rowLogs, x => x.RowNumber == 3 && !x.Success);
    }

    private static ImportService CreateSut(IDbContextFactory<AppDbContext> factory, IExcelParser? parser = null)
    {
        return new ImportService(factory, parser ?? new FakeParser(), NullLogger<ImportService>.Instance);
    }

    private static Dictionary<string, string?> CreateRawRow(int rowNumber, string? artikelnummer)
    {
        return new Dictionary<string, string?>
        {
            ["__RowNumber"] = rowNumber.ToString(),
            ["Artikelnummer"] = artikelnummer
        };
    }

    private static ImportResult<TypeLijst> CreatePreview(params ImportRowResult<TypeLijst>[] rows)
    {
        return new ImportResult<TypeLijst>
        {
            Rows = rows,
            Summary = new ImportSummary
            {
                TotalRows = rows.Length,
                ValidRows = rows.Count(r => r.IsValid),
                InvalidRows = rows.Count(r => !r.IsValid),
                WarningRows = rows.Count(r => r.HasWarnings),
                SkippedCount = rows.Count(r => !r.IsValid)
            },
            GlobalIssues =
            [
                new ImportRowIssue
                {
                    RowNumber = 0,
                    ColumnName = "__EntityName",
                    Message = "TypeLijst",
                    Severity = Severity.Info
                }
            ]
        };
    }

    private static ImportRowResult<TypeLijst> CreateValidRow(int rowNumber, string artikelnummer)
    {
        return new ImportRowResult<TypeLijst>
        {
            RowNumber = rowNumber,
            Parsed = new TypeLijst { Artikelnummer = artikelnummer }
        };
    }

    private static ImportRowResult<TypeLijst> CreateInvalidRow(int rowNumber, string artikelnummer, string message)
    {
        var row = CreateValidRow(rowNumber, artikelnummer);
        row.Issues.Add(new ImportRowIssue
        {
            RowNumber = rowNumber,
            ColumnName = "Artikelnummer",
            Message = message,
            Severity = Severity.Error,
            RawValue = artikelnummer
        });
        return row;
    }

    private sealed class FakeParser : IExcelParser
    {
        private readonly IReadOnlyList<Dictionary<string, string?>> _rows;

        public FakeParser(params Dictionary<string, string?>[] rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyList<Dictionary<string, string?>>> ReadSheetAsync(Stream stream, string sheetName, CancellationToken ct)
            => Task.FromResult(_rows);
    }

    private sealed class NoOpValidator : IImportValidator<TypeLijst>
    {
        public Task ValidateAsync(ImportRowResult<TypeLijst> row, AppDbContext db, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class ExistingTypeLijstValidator : IImportValidator<TypeLijst>
    {
        public async Task ValidateAsync(ImportRowResult<TypeLijst> row, AppDbContext db, CancellationToken ct)
        {
            var artikelnummer = row.Parsed?.Artikelnummer;
            if (string.IsNullOrWhiteSpace(artikelnummer))
            {
                return;
            }

            if (await db.TypeLijsten.AnyAsync(x => x.Artikelnummer == artikelnummer, ct))
            {
                row.Issues.Add(new ImportRowIssue
                {
                    RowNumber = row.RowNumber,
                    ColumnName = "Artikelnummer",
                    Message = "Bestaat al in de database.",
                    Severity = Severity.Error,
                    RawValue = artikelnummer
                });
            }
        }
    }

    private sealed class RecordingCommitter : IImportCommitter<TypeLijst>
    {
        private readonly (int inserted, int updated, int skipped) _result;

        public RecordingCommitter(int inserted, int updated, int skipped)
        {
            _result = (inserted, updated, skipped);
        }

        public List<ImportRowResult<TypeLijst>> ReceivedRows { get; } = [];

        public Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<TypeLijst>> validRows, AppDbContext db, CancellationToken ct)
        {
            ReceivedRows.AddRange(validRows);
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingCommitter : IImportCommitter<TypeLijst>
    {
        public async Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<TypeLijst>> validRows, AppDbContext db, CancellationToken ct)
        {
            db.Leveranciers.Add(new Leverancier { Naam = "ERR" });
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Commit failed on purpose.");
        }
    }

    private sealed class TestTypeLijstMap : IExcelMap<TypeLijst>
    {
        public static TestTypeLijstMap Instance { get; } = new();

        public string EntityName => "TypeLijst";

        public IReadOnlyList<ExcelColumn<TypeLijst>> Columns { get; } =
        [
            new()
            {
                Key = "Artikelnummer",
                Header = "Artikelnummer",
                Required = true,
                Parser = text => string.IsNullOrWhiteSpace(text)
                    ? (false, null, "Artikelnummer is verplicht.")
                    : (true, text, null)
            }
        ];

        public TypeLijst Create() => new();

        public void ApplyCell(TypeLijst target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
        {
            if (columnKey == "Artikelnummer")
            {
                target.Artikelnummer = cellText ?? string.Empty;
            }
        }

        public string? GetCellText(TypeLijst source, string columnKey)
            => columnKey == "Artikelnummer" ? source.Artikelnummer : null;

        public string GetKey(TypeLijst source) => source.Artikelnummer;
    }
}
