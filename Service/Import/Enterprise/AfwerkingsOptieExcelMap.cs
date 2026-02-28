using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Collections.Generic;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class AfwerkingsOptieExcelMap : IExcelMap<AfwerkingsOptie>
{
    public string EntityName => "AfwerkingsOptie";

    public IReadOnlyList<ExcelColumn<AfwerkingsOptie>> Columns { get; } =
    [
        new ExcelColumn<AfwerkingsOptie>
        {
            Key = "Groep",
            Header = "Groep",
            Required = true,
            Parser = value => string.IsNullOrWhiteSpace(value)
                ? (false, null, "Groep is verplicht.")
                : (true, value, null)
        },
        new ExcelColumn<AfwerkingsOptie>
        {
            Key = "Naam",
            Header = "Naam",
            Required = true,
            Parser = value => string.IsNullOrWhiteSpace(value)
                ? (false, null, "Naam is verplicht.")
                : (true, value, null)
        }
    ];

    public AfwerkingsOptie Create() => new();

    public void ApplyCell(AfwerkingsOptie target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        if (columnKey == "Naam")
        {
            target.Omschrijving = cellText?.Trim() ?? string.Empty;
        }
    }

    public string? GetCellText(AfwerkingsOptie source, string columnKey) => columnKey switch
    {
        "Groep" => source.AfwerkingsGroepId.ToString(),
        "Naam" => source.Omschrijving,
        _ => null
    };

    public string GetKey(AfwerkingsOptie source) => $"{source.AfwerkingsGroepId}:{source.Volgnummer}";
}
