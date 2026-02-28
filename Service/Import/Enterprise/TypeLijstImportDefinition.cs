using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstImportDefinition : IImportPreviewDefinition
{
    private readonly IImportService _importService;
    private readonly IExcelMap<TypeLijst> _map;
    private readonly IImportValidator<TypeLijst> _validator;
    private readonly IImportCommitter<TypeLijst> _committer;

    public TypeLijstImportDefinition(
        IImportService importService,
        IExcelMap<TypeLijst> map,
        IImportValidator<TypeLijst> validator,
        IImportCommitter<TypeLijst> committer)
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
        var typedRows = preview.Rows.Select(r => new ImportRowResult<TypeLijst>
        {
            RowNumber = r.RowNumber,
            Parsed = r.Parsed as TypeLijst
        }).ToList();

        foreach (var pair in preview.Rows.Zip(typedRows))
        {
            pair.Item2.Issues.AddRange(pair.Item1.Issues);
        }

        var typedPreview = new QuadroApp.Model.Import.ImportResult<TypeLijst>
        {
            Summary = preview.Summary,
            GlobalIssues = preview.GlobalIssues,
            Rows = typedRows
        };

        return await _importService.CommitAsync(typedPreview, _committer, ct);
    }

    public IReadOnlyDictionary<string, string?> ToDisplayMap(object item)
    {
        var lijst = item as TypeLijst;
        if (lijst is null)
        {
            return new Dictionary<string, string?>();
        }

        return new Dictionary<string, string?>
        {
            ["Artikelnummer"] = lijst.Artikelnummer,
            ["LeverancierCode"] = lijst.Leverancier?.Code,
            ["BreedteCm"] = lijst.BreedteCm.ToString()
        };
    }

    public string GetItemKey(object item)
    {
        var lijst = item as TypeLijst;
        return lijst is null ? string.Empty : _map.GetKey(lijst);
    }
}
