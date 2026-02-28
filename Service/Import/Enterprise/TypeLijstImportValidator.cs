using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstImportValidator : IImportValidator<TypeLijst>
{
    public async Task ValidateAsync(ImportRowResult<TypeLijst> row, AppDbContext db, CancellationToken ct)
    {
        if (row.Parsed is null)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Row",
                Message = "Rij kon niet worden verwerkt.",
                Severity = Severity.Error
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(row.Parsed.Artikelnummer))
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Artikelnummer",
                Message = "Artikelnummer is verplicht.",
                Severity = Severity.Error
            });
        }

        var leverancierCode = row.Parsed.Leverancier?.Code?.Trim();
        if (string.IsNullOrWhiteSpace(leverancierCode))
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "LeverancierCode",
                Message = "LeverancierCode is verplicht.",
                Severity = Severity.Error
            });
            return;
        }

        var leverancierExists = await db.Leveranciers
            .AnyAsync(l => l.Code == leverancierCode, ct);

        if (!leverancierExists)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "LeverancierCode",
                Message = $"Onbekende leveranciercode: {leverancierCode}.",
                Severity = Severity.Error,
                RawValue = leverancierCode
            });
        }
    }
}
