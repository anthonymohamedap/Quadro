using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class AfwerkingsOptieImportValidator : IImportValidator<AfwerkingsOptie>
{
    public async Task ValidateAsync(ImportRowResult<AfwerkingsOptie> row, AppDbContext db, CancellationToken ct)
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
            return;
        }

        if (string.IsNullOrWhiteSpace(row.Parsed.Naam))
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Naam",
                Message = "Naam is verplicht.",
                Severity = Severity.Error
            });
        }

        if (row.Parsed.Volgnummer == default)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Volgnummer",
                Message = "Volgnummer is verplicht.",
                Severity = Severity.Error
            });
        }

        var groepCode = row.Parsed.AfwerkingsGroep?.Code;
        if (!groepCode.HasValue)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Groep",
                Message = "Groepcode is verplicht.",
                Severity = Severity.Error
            });
            return;
        }

        var groepExists = await db.AfwerkingsGroepen.AnyAsync(g => g.Code == groepCode.Value, ct);
        if (!groepExists)
        {
            row.Issues.Add(new ImportRowIssue
            {
                RowNumber = row.RowNumber,
                ColumnName = "Groep",
                Message = $"Onbekende groepcode: {groepCode.Value}.",
                Severity = Severity.Error,
                RawValue = groepCode.Value.ToString()
            });
        }
    }
}
