using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class KlantExcelMap : IExcelMap<Klant>
{
    public string EntityName => "Klant";

    public IReadOnlyList<ExcelColumn<Klant>> Columns { get; } =
    [
        CreateText("Voornaam", required: true),
        CreateText("Achternaam", required: true),
        CreateText("Email"),
        CreateText("Telefoon"),
        CreateText("Straat"),
        CreateText("Nummer"),
        CreateText("Postcode"),
        CreateText("Gemeente"),
        CreateText("BtwNummer"),
        CreateText("Opmerking")
    ];

    public Klant Create() => new();

    public void ApplyCell(Klant target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        var value = string.IsNullOrWhiteSpace(cellText) ? null : cellText.Trim();

        switch (columnKey)
        {
            case "Voornaam":
                target.Voornaam = value ?? string.Empty;
                break;
            case "Achternaam":
                target.Achternaam = value ?? string.Empty;
                break;
            case "Email":
                target.Email = value;
                break;
            case "Telefoon":
                target.Telefoon = value;
                break;
            case "Straat":
                target.Straat = value;
                break;
            case "Nummer":
                target.Nummer = value;
                break;
            case "Postcode":
                target.Postcode = value;
                break;
            case "Gemeente":
                target.Gemeente = value;
                break;
            case "BtwNummer":
                target.BtwNummer = value;
                break;
            case "Opmerking":
                target.Opmerking = value;
                break;
            default:
                issues.Add(new ImportRowIssue
                {
                    RowNumber = rowNumber,
                    ColumnName = columnKey,
                    Message = "Onbekende kolom in mapping.",
                    Severity = Severity.Warning,
                    RawValue = cellText
                });
                break;
        }
    }

    public string? GetCellText(Klant source, string columnKey) => columnKey switch
    {
        "Voornaam" => source.Voornaam,
        "Achternaam" => source.Achternaam,
        "Email" => source.Email,
        "Telefoon" => source.Telefoon,
        "Straat" => source.Straat,
        "Nummer" => source.Nummer,
        "Postcode" => source.Postcode,
        "Gemeente" => source.Gemeente,
        "BtwNummer" => source.BtwNummer,
        "Opmerking" => source.Opmerking,
        _ => null
    };

    public string GetKey(Klant source)
    {
        if (!string.IsNullOrWhiteSpace(source.Email))
        {
            return source.Email.Trim().ToLowerInvariant();
        }

        return $"{source.Voornaam}|{source.Achternaam}|{source.Telefoon}".Trim().ToLowerInvariant();
    }

    private static ExcelColumn<Klant> CreateText(string key, bool required = false) => new()
    {
        Key = key,
        Header = key,
        Required = required,
        Parser = value =>
        {
            if (required && string.IsNullOrWhiteSpace(value))
            {
                return (false, null, $"Kolom {key} is verplicht.");
            }

            return (true, string.IsNullOrWhiteSpace(value) ? null : value.Trim(), null);
        }
    };
}
