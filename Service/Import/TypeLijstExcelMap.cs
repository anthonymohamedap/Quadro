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
        Text("Leverancier", true, "LeverancierCode"),
        Text("Levcode", false, "SupplierArticleCode"),
        Text("Soort", false, "Type"),
        Number("BreedteCm", true, "Breedte"),
        Decimal("VoorraadMeter", false, "Stock"),
        Decimal("MinimumVoorraad", false, "Minstock", "MinimumVoorraad"),
        Decimal("InventarisKost", false, "Inventariskost", "InventarisKost"),
        Text("Opmerking1", false, "Opmerking1"),
        Text("Opmerking2", false, "Opmerking2")
    ];

    public TypeLijst Create() => new();

    public void ApplyCell(TypeLijst target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues)
    {
        switch (columnKey)
        {
            case "Artikelnummer":
                target.Artikelnummer = cellText?.Trim() ?? string.Empty;
                break;
            case "Leverancier":
                var naam = NormalizeLeverancierNaam(cellText);
                if (!string.IsNullOrWhiteSpace(naam))
                {
                    target.Leverancier = new Leverancier { Naam = naam };
                }
                break;
            case "Levcode":
                target.Levcode = (cellText ?? string.Empty).Trim();
                break;
            case "Soort":
                target.Soort = cellText?.Trim() ?? string.Empty;
                break;
            case "BreedteCm":
                if (TryParseDecimal(cellText, out var breedte)) target.BreedteCm = (int)Math.Round(breedte);
                break;
            case "VoorraadMeter":
                if (TryParseDecimal(cellText, out var voorraadMeter)) target.VoorraadMeter = voorraadMeter;
                break;
            case "MinimumVoorraad":
                if (TryParseDecimal(cellText, out var minimumVoorraad)) target.MinimumVoorraad = minimumVoorraad;
                break;
            case "InventarisKost":
                if (TryParseDecimal(cellText, out var inventarisKost)) target.InventarisKost = inventarisKost;
                break;
            case "Opmerking1":
                target.Opmerking = MergeOpmerking(cellText, target.Opmerking, 1);
                break;
            case "Opmerking2":
                target.Opmerking = MergeOpmerking(cellText, target.Opmerking, 2);
                break;
        }
    }

    public string? GetCellText(TypeLijst source, string columnKey) => columnKey switch
    {
        "Artikelnummer" => source.Artikelnummer,
        "Leverancier" => source.Leverancier?.Naam,
        "Levcode" => source.Levcode,
        "Soort" => source.Soort,
        "BreedteCm" => source.BreedteCm.ToString(CultureInfo.InvariantCulture),
        "VoorraadMeter" => source.VoorraadMeter.ToString(CultureInfo.InvariantCulture),
        "MinimumVoorraad" => source.MinimumVoorraad.ToString(CultureInfo.InvariantCulture),
        "InventarisKost" => source.InventarisKost.ToString(CultureInfo.InvariantCulture),
        "Opmerking1" => source.Opmerking,
        "Opmerking2" => string.Empty,
        _ => null
    };

    public string GetKey(TypeLijst source) => source.Artikelnummer?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeLeverancierNaam(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();

    private static string MergeOpmerking(string? value, string existing, int part)
    {
        var firstPart = existing;
        var secondPart = string.Empty;

        if (!string.IsNullOrWhiteSpace(existing) && existing.Contains('/'))
        {
            var split = existing.Split('/', 2, StringSplitOptions.TrimEntries);
            firstPart = split[0];
            secondPart = split.Length > 1 ? split[1] : string.Empty;
        }

        if (part == 1)
        {
            firstPart = value?.Trim() ?? string.Empty;
        }
        else
        {
            secondPart = value?.Trim() ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(secondPart)
            ? firstPart
            : $"{firstPart}/{secondPart}";
    }

    private static bool TryParseInt(string? input, out int value)
    {
        value = 0;
        if (!TryParseDecimal(input, out var dec)) return false;
        value = (int)Math.Round(dec);
        return true;
    }

    private static bool TryParseDecimal(string? input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var s = input.Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out value)) return true;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
        return false;
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

            return TryParseDecimal(value, out _)
                ? (true, value, null)
                : (false, null, $"Kolom {key} bevat geen geldig getal.");
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
}
