using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class AfwerkingsOptieExcelMap : IExcelMap<AfwerkingsOptie>
{
    public string EntityName => "AfwerkingsOptie";

    public IReadOnlyList<ExcelColumn<AfwerkingsOptie>> Columns { get; } =
    [
        Text("Groep", true, "Afwerking"),
        Text("Naam", true),
        Text("Volgnummer", true, "Volgnumm"),
        Decimal("KostprijsPerM2", false, "KostprijsPe", "KostprijsPerM2"),
        Decimal("WinstMarge", false, "WinstMarg", "WinstMarge"),
        Decimal("AfvalPercentage", false, "AfvalPerce", "AfvalPercentage"),
        Decimal("VasteKost", false),
        Number("WerkMinuten", false, "WerkMinu"),
        Text("LeverancierCode", false)
    ];

    public AfwerkingsOptie Create() => new();

    public void ApplyCell(AfwerkingsOptie target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        switch (columnKey)
        {
            case "Groep":
                var groepCode = ParseGroepCode(cellText);
                if (groepCode.HasValue)
                {
                    target.AfwerkingsGroep = new AfwerkingsGroep { Code = groepCode.Value };
                }
                break;
            case "Naam":
                target.Naam = cellText?.Trim() ?? string.Empty;
                break;
            case "Volgnummer":
                var volgnummer = ParseVolgnummer(cellText);
                if (volgnummer.HasValue)
                {
                    target.Volgnummer = volgnummer.Value;
                }
                break;
            case "KostprijsPerM2":
                if (TryParseDecimal(cellText, out var kostprijs))
                {
                    target.KostprijsPerM2 = kostprijs;
                }
                break;
            case "WinstMarge":
                if (TryParseDecimal(cellText, out var marge))
                {
                    target.WinstMarge = marge;
                }
                break;
            case "AfvalPercentage":
                if (TryParseDecimal(cellText, out var afval))
                {
                    target.AfvalPercentage = afval;
                }
                break;
            case "VasteKost":
                if (TryParseDecimal(cellText, out var vasteKost))
                {
                    target.VasteKost = vasteKost;
                }
                break;
            case "WerkMinuten":
                if (TryParseInt(cellText, out var werkMinuten))
                {
                    target.WerkMinuten = werkMinuten;
                }
                break;
            case "LeverancierCode":
                var leverancierCode = cellText?.Trim();
                if (!string.IsNullOrWhiteSpace(leverancierCode))
                {
                    target.Leverancier = new Leverancier { Code = leverancierCode };
                }
                break;
        }
    }

    public string? GetCellText(AfwerkingsOptie source, string columnKey) => columnKey switch
    {
        "Groep" => source.AfwerkingsGroep is not null ? source.AfwerkingsGroep.Code.ToString() : null,
        "Naam" => source.Naam,
        "Volgnummer" => source.Volgnummer == default ? string.Empty : source.Volgnummer.ToString(),
        "KostprijsPerM2" => source.KostprijsPerM2.ToString(CultureInfo.InvariantCulture),
        "WinstMarge" => source.WinstMarge.ToString(CultureInfo.InvariantCulture),
        "AfvalPercentage" => source.AfvalPercentage.ToString(CultureInfo.InvariantCulture),
        "VasteKost" => source.VasteKost.ToString(CultureInfo.InvariantCulture),
        "WerkMinuten" => source.WerkMinuten.ToString(CultureInfo.InvariantCulture),
        "LeverancierCode" => source.Leverancier?.Code,
        _ => null
    };

    public string GetKey(AfwerkingsOptie source) => $"{source.AfwerkingsGroep?.Code}:{source.Volgnummer}";

    private static char? ParseGroepCode(string? value)
    {
        var raw = value?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.ToUpperInvariant() switch
        {
            "G" or "GLAS" => 'G',
            "P" or "PASSE-PARTOUT" or "PASSEPARTOUT" => 'P',
            "D" or "DIEPTEKERN" or "DIEPTE KERN" => 'D',
            "O" or "OPKLEVEN" => 'O',
            "R" or "RUG" => 'R',
            _ when raw.Length == 1 => char.ToUpperInvariant(raw[0]),
            _ => null
        };
    }

    private static char? ParseVolgnummer(string? value)
    {
        var raw = value?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return char.ToUpperInvariant(raw[0]);
    }

    private static ExcelColumn<AfwerkingsOptie> Text(string key, bool required = false, params string[] aliases) => new()
    {
        Key = key,
        Header = key,
        Aliases = aliases,
        Required = required,
        Parser = value => !required || !string.IsNullOrWhiteSpace(value)
            ? (true, value, null)
            : (false, null, $"Kolom {key} is verplicht.")
    };

    private static ExcelColumn<AfwerkingsOptie> Number(string key, bool required = false, params string[] aliases) => new()
    {
        Key = key,
        Header = key,
        Aliases = aliases,
        Required = required,
        Parser = value =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return required ? (false, null, $"Kolom {key} is verplicht.") : (true, null, null);
            }

            return TryParseInt(value, out _)
                ? (true, value, null)
                : (false, null, $"Kolom {key} bevat geen geldig geheel getal.");
        }
    };

    private static ExcelColumn<AfwerkingsOptie> Decimal(string key, bool required = false, params string[] aliases) => new()
    {
        Key = key,
        Header = key,
        Aliases = aliases,
        Required = required,
        Parser = value =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return required ? (false, null, $"Kolom {key} is verplicht.") : (true, null, null);
            }

            return TryParseDecimal(value, out _)
                ? (true, value, null)
                : (false, null, $"Kolom {key} bevat geen geldig decimaal getal.");
        }
    };

    private static bool TryParseInt(string? value, out int parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return int.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out parsed)
               || int.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseDecimal(string? value, out decimal parsed)
    {
        parsed = 0m;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out parsed)
               || decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
    }
}
