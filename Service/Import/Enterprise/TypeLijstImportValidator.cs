using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstImportValidator : IImportValidator<TypeLijst>
{
    public Task ValidateAsync(ImportRowResult<TypeLijst> row, AppDbContext db, CancellationToken ct)
    {
        if (row.Parsed is null)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Rij",
                Message = "Rij kon niet worden verwerkt.",
                Severity = Severity.Error
            });
            return Task.CompletedTask;
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

        if (row.Parsed.BreedteCm <= 0)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "BreedteCm",
                Message = "Breedte moet groter zijn dan 0.",
                Severity = Severity.Error,
                RawValue = row.Parsed.BreedteCm.ToString()
            });
        }

        return Task.CompletedTask;
    }
}
