using ClosedXML.Excel;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.Globalization;

namespace QuadroApp.Services.Import;

public static class ExcelRowMapper
{
    public static TypeLijstPreviewRow TryMapPreview(
        IXLRow r,
        Dictionary<string, Leverancier> leveranciers,
        int rowNumber)
    {
        var artikelnummer = r.Cell(1).GetString()?.Trim() ?? "";
        var leverancierCode = r.Cell(2).GetString()?.Trim() ?? "";

        var preview = new TypeLijstPreviewRow
        {
            RowNumber = rowNumber,
            Artikelnummer = artikelnummer,
            LeverancierCode = leverancierCode,
            BreedteCm = (int)ReadDecimalOrDefault(r.Cell(6), 0m),
            Type = r.Cell(5).GetString()?.Trim() ?? "",
            Opmerking1 = r.Cell(7).GetString()?.Trim() ?? "",
            Opmerking2 = r.Cell(8).GetString()?.Trim() ?? "",
            LeverancierNaam = r.Cell(4).GetString()?.Trim() ?? "",
            Stock = ReadDecimalOrDefault(r.Cell(9), 0m),
            Minstock = ReadDecimalOrDefault(r.Cell(10), 0m),
            Inventariskost = ReadDecimalOrDefault(r.Cell(11), 0m),
        };

        // Validatie
        if (string.IsNullOrWhiteSpace(preview.Artikelnummer))
        {
            preview.IsValid = false;
            preview.ValidationMessage = "Code ontbreekt";
            return preview;
        }



        preview.IsValid = true;
        preview.ValidationMessage = null;
        return preview;
    }

    private static decimal ReadDecimalOrDefault(IXLCell cell, decimal defaultValue = 0m)
    {
        if (cell.IsEmpty()) return defaultValue;

        var raw = cell.Value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;

        raw = raw.Replace("€", "").Replace("EUR", "").Trim();

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out var be))
            return be;

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
            return inv;

        return defaultValue;
    }
}
