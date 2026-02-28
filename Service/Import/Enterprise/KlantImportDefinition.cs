using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class KlantImportDefinition : IImportPreviewDefinition
{
    private readonly IImportService _importService;
    private readonly IExcelMap<Klant> _map;
    private readonly IImportValidator<Klant> _validator;
    private readonly IImportCommitter<Klant> _committer;

    public KlantImportDefinition(
        IImportService importService,
        IExcelMap<Klant> map,
        IImportValidator<Klant> validator,
        IImportCommitter<Klant> committer)
    {
        _importService = importService;
        _map = map;
        _validator = validator;
        _committer = committer;
    }

    public string EntityName => _map.EntityName;

    public async Task<QuadroApp.Model.Import.ImportResult<object>> DryRunAsync(Stream stream, CancellationToken ct)
    {
        var result = await _importService.DryRunAsync(stream, _map, _validator, ct);
        var convertedRows = result.Rows.Select(r =>
        {
            var converted = new ImportRowResult<object>
            {
                RowNumber = r.RowNumber,
                Parsed = r.Parsed
            };
            converted.Issues.AddRange(r.Issues);
            return converted;
        }).ToList();

        return new QuadroApp.Model.Import.ImportResult<object>
        {
            Summary = result.Summary,
            GlobalIssues = result.GlobalIssues,
            Rows = convertedRows
        };
    }

    public async Task<ImportCommitReceipt> CommitAsync(QuadroApp.Model.Import.ImportResult<object> preview, CancellationToken ct)
    {
        var typedRows = preview.Rows.Select(r => new ImportRowResult<Klant>
        {
            RowNumber = r.RowNumber,
            Parsed = r.Parsed as Klant
        }).ToList();

        foreach (var pair in preview.Rows.Zip(typedRows))
        {
            pair.Item2.Issues.AddRange(pair.Item1.Issues);
        }

        var typedPreview = new QuadroApp.Model.Import.ImportResult<Klant>
        {
            Summary = preview.Summary,
            GlobalIssues = preview.GlobalIssues,
            Rows = typedRows
        };

        return await _importService.CommitAsync(typedPreview, _committer, ct);
    }

    public IReadOnlyDictionary<string, string?> ToDisplayMap(object item)
    {
        var klant = item as Klant;
        if (klant is null)
        {
            return new Dictionary<string, string?>();
        }

        return new Dictionary<string, string?>
        {
            ["Voornaam"] = klant.Voornaam,
            ["Achternaam"] = klant.Achternaam,
            ["Email"] = klant.Email,
            ["Telefoon"] = klant.Telefoon,
            ["Straat"] = klant.Straat,
            ["Nummer"] = klant.Nummer,
            ["Postcode"] = klant.Postcode,
            ["Gemeente"] = klant.Gemeente
        };
    }

    public string GetItemKey(object item)
    {
        var klant = item as Klant;
        if (klant is null)
        {
            return string.Empty;
        }

        return _map.GetKey(klant);
    }
}
