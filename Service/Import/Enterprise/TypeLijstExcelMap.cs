using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstExcelMap : IExcelMap<TypeLijst>
{
    public string EntityName => "TypeLijst";

    public IReadOnlyList<ExcelColumn<TypeLijst>> Columns { get; } =
    [
        Text("Artikelnummer", true, "Code"),
        Text("LeverancierCode", false, "Levcode"),
        Text("Soort", false, "Type"),
        Number("BreedteCm", true, "Breedte"),
        Decimal("PrijsPerMeter", false, "KostprijsPerM", "Kostprijs", "PrijsPerMeter"),
        Decimal("WinstMargeFactor", false, "WinstMarge"),
        Decimal("AfvalPercentage", false, "AfvalPerce", "AfvalPercentage"),
        Decimal("VasteKost", false, "VasteKost"),
        Number("WerkMinuten", false, "WerkMinu", "WerkMinuten"),
        Decimal("VoorraadMeter", false, "Stock"),
        Decimal("MinimumVoorraad", false, "Minstock", "MinimumVoorraad"),
        Decimal("InventarisKost", false, "Inventariskost", "InventarisKost"),
        Text("Opmerking1", false, "Opmerking1"),
        Text("Opmerking2", false, "Opmerking2"),
        Text("Serie", false, "Serie")
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
            case "Soort":
                target.Soort = cellText?.Trim() ?? string.Empty;
                break;
            case "BreedteCm":
                if (TryParseInt(cellText, out var breedte))
                {
                    target.BreedteCm = breedte;
                }
                break;
            case "PrijsPerMeter":
                if (TryParseDecimal(cellText, out var prijsPerMeter))
                {
                    target.PrijsPerMeter = prijsPerMeter;
                }
                break;
            case "WinstMargeFactor":
                if (TryParseDecimal(cellText, out var winstMarge))
                {
                    target.WinstMargeFactor = winstMarge;
                }
                break;
            case "AfvalPercentage":
                if (TryParseDecimal(cellText, out var afvalPercentage))
                {
                    target.AfvalPercentage = afvalPercentage;
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
            case "VoorraadMeter":
                if (TryParseDecimal(cellText, out var voorraadMeter))
                {
                    target.VoorraadMeter = voorraadMeter;
                }
                break;
            case "MinimumVoorraad":
                if (TryParseDecimal(cellText, out var minimumVoorraad))
                {
                    target.MinimumVoorraad = minimumVoorraad;
                }
                break;
            case "InventarisKost":
                if (TryParseDecimal(cellText, out var inventarisKost))
                {
                    target.InventarisKost = inventarisKost;
                }
                break;
            case "Opmerking1":
                target.Opmerking = MergeOpmerking(cellText, target.Opmerking, 1);
                break;
            case "Opmerking2":
                target.Opmerking = MergeOpmerking(cellText, target.Opmerking, 2);
                break;
            case "Serie":
                target.Serie = string.IsNullOrWhiteSpace(cellText) ? null : cellText.Trim();
                break;
        }
    }

    public string? GetCellText(TypeLijst source, string columnKey) => columnKey switch
    {
        "Artikelnummer" => source.Artikelnummer,
        "LeverancierCode" => source.LeverancierCode,
        "Soort" => source.Soort,
        "BreedteCm" => source.BreedteCm.ToString(CultureInfo.InvariantCulture),
        "PrijsPerMeter" => source.PrijsPerMeter.ToString(CultureInfo.InvariantCulture),
        "WinstMargeFactor" => source.WinstMargeFactor.ToString(CultureInfo.InvariantCulture),
        "AfvalPercentage" => source.AfvalPercentage.ToString(CultureInfo.InvariantCulture),
        "VasteKost" => source.VasteKost.ToString(CultureInfo.InvariantCulture),
        "WerkMinuten" => source.WerkMinuten.ToString(CultureInfo.InvariantCulture),
        "VoorraadMeter" => source.VoorraadMeter.ToString(CultureInfo.InvariantCulture),
        "MinimumVoorraad" => source.MinimumVoorraad.ToString(CultureInfo.InvariantCulture),
        "InventarisKost" => source.InventarisKost.ToString(CultureInfo.InvariantCulture),
        "Opmerking1" => source.Opmerking,
        "Opmerking2" => string.Empty,
        "Serie" => source.Serie,
        _ => null
    };

    public string GetKey(TypeLijst source) => source.Artikelnummer?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string MergeOpmerking(string? input, string existing, int part)
    {
        var text = string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim();
        var current = existing.Split('/', 2, StringSplitOptions.TrimEntries);
        var first = current.Length > 0 ? current[0] : string.Empty;
        var second = current.Length > 1 ? current[1] : string.Empty;

        if (part == 1)
        {
            first = text;
        }
        else
        {
            second = text;
        }

        return string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second)
            ? string.Empty
            : $"{first}/{second}".Trim('/');
    }

    private static ExcelColumn<TypeLijst> Text(string key, bool required = false, params string[] aliases) => new()
    {
        Key = key,
        Header = key,
        Aliases = aliases,
        Required = required,
        Parser = value => !required || !string.IsNullOrWhiteSpace(value)
            ? (true, value, null)
            : (false, null, $"Kolom {key} is verplicht.")
    };

    private static ExcelColumn<TypeLijst> Number(string key, bool required = false, params string[] aliases) => new()
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

    private static ExcelColumn<TypeLijst> Decimal(string key, bool required = false, params string[] aliases) => new()
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

    private static bool TryParseDecimal(string? value, out decimal parsed)
    {
        parsed = 0m;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out parsed)
               || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseInt(string? value, out int parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        if (int.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out parsed)
            || int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
        {
            return true;
        }

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out var decBe))
        {
            parsed = (int)Math.Round(decBe);
            return true;
        }

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var decInv))
        {
            parsed = (int)Math.Round(decInv);
            return true;
        }

        return false;
    }
}
