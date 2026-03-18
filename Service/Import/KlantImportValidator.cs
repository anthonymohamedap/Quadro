using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class KlantImportValidator : IImportValidator<Klant>
{
    public async Task ValidateAsync(ImportRowResult<Klant> row, AppDbContext db, CancellationToken ct)
    {
        if (row.Parsed is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(row.Parsed.Voornaam))
        {
            row.Issues.Add(new ImportRowIssue { RowNumber = row.RowNumber, ColumnName = "Voornaam", Message = "Voornaam is verplicht.", Severity = Severity.Error });
        }

        if (string.IsNullOrWhiteSpace(row.Parsed.Achternaam))
        {
            row.Issues.Add(new ImportRowIssue { RowNumber = row.RowNumber, ColumnName = "Achternaam", Message = "Achternaam is verplicht.", Severity = Severity.Error });
        }

        if (!string.IsNullOrWhiteSpace(row.Parsed.Email))
        {
            var exists = await db.Klanten.AnyAsync(k => k.Email != null && k.Email.ToLower() == row.Parsed.Email.ToLower(), ct);
            if (exists)
            {
                row.Issues.Add(new ImportRowIssue
                {
                    RowNumber = row.RowNumber,
                    ColumnName = "Email",
                    Message = "Klant bestaat reeds in de database en wordt bijgewerkt.",
                    Severity = Severity.Warning,
                    RawValue = row.Parsed.Email
                });
            }
        }
    }
}
