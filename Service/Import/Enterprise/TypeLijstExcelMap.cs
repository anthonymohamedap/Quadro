using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstExcelMap : IExcelMap<TypeLijst>
{
    public string EntityName => "TypeLijst";

    public IReadOnlyList<ExcelColumn<TypeLijst>> Columns { get; } =
    [
        Text("Artikelnummer", true),
        Text("LeverancierCode", true),
        Number("BreedteCm")
    ];

    public TypeLijst Create() => new();

    public void ApplyCell(TypeLijst target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        switch (columnKey)
        {
            case "Artikelnummer":
                target.Artikelnummer = cellText?.Trim() ?? string.Empty;
                break;
            case "LeverancierCode":
                var code = cellText?.Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    target.Leverancier = new Leverancier { Code = code };
                }
                break;
            case "BreedteCm":
                if (int.TryParse(cellText, out var width))
                {
                    target.BreedteCm = width;
                }
                else if (!string.IsNullOrWhiteSpace(cellText))
                {
                    issues.Add(new ImportRowIssue
                    {
                        RowNumber = rowNumber,
                        ColumnName = columnKey,
                        Message = "BreedteCm is geen geldig geheel getal.",
                        Severity = Severity.Error,
                        RawValue = cellText
                    });
                }
                break;
        }
    }

    public string? GetCellText(TypeLijst source, string columnKey) => columnKey switch
    {
        "Artikelnummer" => source.Artikelnummer,
        "LeverancierCode" => source.LeverancierCode,
        "BreedteCm" => source.BreedteCm.ToString(),
        _ => null
    };

    public string GetKey(TypeLijst source) => source.Artikelnummer?.Trim().ToLowerInvariant() ?? string.Empty;

    private static ExcelColumn<TypeLijst> Text(string key, bool required = false) => new()
    {
        Key = key,
        Header = key,
        Required = required,
        Parser = value => !required || !string.IsNullOrWhiteSpace(value)
            ? (true, value, null)
            : (false, null, $"Kolom {key} is verplicht.")
    };

    private static ExcelColumn<TypeLijst> Number(string key) => new()
    {
        Key = key,
        Header = key,
        Parser = value => string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _)
            ? (true, value, null)
            : (false, null, $"Kolom {key} bevat geen geldig geheel getal.")
    };
}
