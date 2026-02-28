using ClosedXML.Excel;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuadroApp.Service.Import;

public static class AfwerkingsOptieExcelRowMapper
{
    private static readonly HashSet<string> AllowedGroepen =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Glas",
            "Passe-partout",
            "Dieptekern",
            "Opkleven",
            "Rug"
        };

    public static AfwerkingsOptiePreviewRow Map(IXLRow r, int row)
    {
        var p = new AfwerkingsOptiePreviewRow
        {
            RowNumber = row,
            AfwerkingsGroep = r.Cell(1).GetString().Trim(),
            Naam = r.Cell(2).GetString().Trim(),

            // ✅ VOLGNUMMER als string + char (1-9, A-K)
            VolgnummerRaw = r.Cell(3).GetString().Trim(),

            KostprijsPerM2 = ReadDecimal(r.Cell(4)),
            WinstMarge = ReadDecimal(r.Cell(5), 0.25m),
            AfvalPercentage = ReadDecimal(r.Cell(6)),
            VasteKost = ReadDecimal(r.Cell(7)),
            WerkMinuten = ReadInt(r.Cell(8)),
            LeverancierCode = r.Cell(9).GetString().Trim()
        };

        // 🔍 VALIDATIE

        if (string.IsNullOrWhiteSpace(p.AfwerkingsGroep))
        {
            p.IsValid = false;
            p.ValidationMessage = "AfwerkingsGroep ontbreekt";
            return p;
        }

        if (!AllowedGroepen.Contains(p.AfwerkingsGroep))
        {
            p.IsValid = false;
            p.ValidationMessage = $"Ongeldige AfwerkingsGroep: {p.AfwerkingsGroep}";
            return p;
        }

        if (string.IsNullOrWhiteSpace(p.Naam))
        {
            p.IsValid = false;
            p.ValidationMessage = "Naam ontbreekt";
            return p;
        }

        // ✅ Parse + validate volgnummer
        if (!TryParseVolgnummer(p.VolgnummerRaw, out var volgChar))
        {
            p.IsValid = false;
            p.ValidationMessage = "Volgnummer moet 1-9 of A-K zijn";
            return p;
        }

        p.Volgnummer = volgChar;

        // (optioneel) ranges voor prijzen
        if (p.KostprijsPerM2 < 0 || p.VasteKost < 0)
        {
            p.IsValid = false;
            p.ValidationMessage = "Kostprijzen mogen niet negatief zijn";
            return p;
        }

        if (p.AfvalPercentage < 0 || p.AfvalPercentage > 100)
        {
            p.IsValid = false;
            p.ValidationMessage = "AfvalPercentage moet tussen 0 en 100 liggen";
            return p;
        }

        if (p.WerkMinuten < 0 || p.WerkMinuten > 1440)
        {
            p.IsValid = false;
            p.ValidationMessage = "WerkMinuten moet tussen 0 en 1440 liggen";
            return p;
        }

        p.IsValid = true;
        return p;
    }

    private static bool TryParseVolgnummer(string? raw, out char volg)
    {
        volg = '1';
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();

        // neem eerste teken, maakt niet uit of user "A1" of "A" typt
        var c = char.ToUpperInvariant(s[0]);

        // Toegelaten: 1-9 en A-K
        var ok = (c >= '1' && c <= '9') || (c >= 'A' && c <= 'K');
        if (!ok) return false;

        volg = c;
        return true;
    }

    // jouw bestaande helpers (laten staan)
    private static int ReadInt(IXLCell cell)
    {
        var raw = cell.GetString()?.Trim();
        if (int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
            return i;

        // als het een nummercel is:
        if (cell.TryGetValue<double>(out var d))
            return (int)Math.Round(d);

        return 0;
    }

    private static decimal ReadDecimal(IXLCell cell, decimal defaultValue = 0m)
    {
        if (cell.IsEmpty()) return defaultValue;

        var raw = cell.Value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;

        raw = raw.Replace("€", "").Replace("EUR", "").Trim();

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("nl-BE"), out var be))
            return be;

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
            return inv;

        // als het een nummercel is:
        if (cell.TryGetValue<double>(out var d))
            return (decimal)d;

        return defaultValue;
    }




}
