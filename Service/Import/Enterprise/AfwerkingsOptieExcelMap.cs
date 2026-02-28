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
            Parser = value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return (false, null, "Groep is verplicht.");
                }

                var text = value.Trim();
                return text.Length == 1
                    ? (true, text, null)
                    : (false, null, "Groep moet 1 karakter zijn.");
            }
        },
        new ExcelColumn<AfwerkingsOptie>
        {
            Key = "Naam",
            Header = "Naam",
            Required = true,
            Parser = value => string.IsNullOrWhiteSpace(value)
                ? (false, null, "Naam is verplicht.")
                : (true, value, null)
        },
        new ExcelColumn<AfwerkingsOptie>
        {
            Key = "Volgnummer",
            Header = "Volgnummer",
            Required = false,
            Parser = value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return (true, null, null);
                }

                var text = value.Trim();
                return text.Length == 1
                    ? (true, text, null)
                    : (false, null, "Volgnummer moet 1 karakter zijn.");
            }
        }
    ];

    public AfwerkingsOptie Create() => new();

    public void ApplyCell(AfwerkingsOptie target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        switch (columnKey)
        {
            case "Groep":
                var groepCodeText = cellText?.Trim();
                if (!string.IsNullOrWhiteSpace(groepCodeText))
                {
                    target.AfwerkingsGroep = new AfwerkingsGroep { Code = groepCodeText[0] };
                }
                break;
            case "Naam":
                target.Naam = cellText?.Trim() ?? string.Empty;
                break;
            case "Volgnummer":
                var value = cellText?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Volgnummer = value[0];
                }
                break;
        }
    }

    string? IExcelMap<AfwerkingsOptie>.GetCellText(AfwerkingsOptie source, string columnKey) => GetCellTextValue(source, columnKey);

    private static string? GetCellTextValue(AfwerkingsOptie source, string columnKey) => columnKey switch
    {
        "Groep" => source.AfwerkingsGroep is not null ? source.AfwerkingsGroep.Code.ToString() : source.AfwerkingsGroepId.ToString(),
        "Naam" => source.Naam,
        "Volgnummer" => source.Volgnummer == default ? string.Empty : source.Volgnummer.ToString(),
        _ => null
    };

    public string GetKey(AfwerkingsOptie source) => $"{source.AfwerkingsGroepId}:{source.Volgnummer}";
}
