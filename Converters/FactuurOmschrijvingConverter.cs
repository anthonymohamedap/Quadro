using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuadroApp.Converters
{
    /// <summary>
    /// Zet de getagde pipe-omschrijving van een FactuurLijn om naar leesbare,
    /// meerregelige tekst. Bv:
    ///   "001002/A2 | 30x40 cm | titel:ww | glas:Helder glas 2 mm | lijst_opm:Witte lijst | afhaal:2026-05-08"
    /// wordt:
    ///   ww  (001002/A2)  ·  30x40 cm
    ///   Glas: Helder glas 2 mm
    ///   Lijst: Witte lijst
    ///   Afhalen: 08/05/2026
    /// </summary>
    public class FactuurOmschrijvingConverter : IValueConverter
    {
        public static readonly FactuurOmschrijvingConverter Instance = new();

        private static readonly Dictionary<string, string> TagLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["glas:"]     = "Glas",
            ["pp1:"]      = "Passe-partout 1",
            ["pp2:"]      = "Passe-partout 2",
            ["diepte:"]   = "Dieptekern",
            ["opkleven:"] = "Opkleven",
            ["rug:"]      = "Rug",
        };

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string raw || string.IsNullOrWhiteSpace(raw))
                return value;

            // Geen tags? Toon ongewijzigd.
            if (!raw.Contains('|') && !raw.Contains(':'))
                return raw;

            var parts = raw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return raw;

            var artikel  = parts.ElementAtOrDefault(0);
            var afmeting = parts.ElementAtOrDefault(1);

            string? titel = null, lijstOpm = null, regelOpm = null, afhaal = null;
            var afwerkingen = new List<string>();
            var overig = new List<string>();

            foreach (var part in parts.Skip(2))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                var idx = part.IndexOf(':');
                if (idx <= 0)
                {
                    overig.Add(part);
                    continue;
                }

                var tag = part.Substring(0, idx + 1).ToLowerInvariant();
                var val = part[(idx + 1)..].Trim();
                if (val.Length == 0) continue;

                switch (tag)
                {
                    case "titel:":     titel = val; break;
                    case "lijst_opm:": lijstOpm = val; break;
                    case "opm:":       regelOpm = val; break;
                    case "afhaal:":
                        afhaal = DateTime.TryParseExact(val, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var d)
                            ? d.ToString("dd/MM/yyyy")
                            : val;
                        break;
                    default:
                        if (TagLabels.TryGetValue(tag, out var label))
                            afwerkingen.Add($"{label}: {val}");
                        else
                            overig.Add(part);
                        break;
                }
            }

            var lines = new List<string>();

            var kop = !string.IsNullOrWhiteSpace(titel) ? titel! : (artikel ?? "Werkstuk");
            if (!string.IsNullOrWhiteSpace(titel) && !string.IsNullOrWhiteSpace(artikel))
                kop = $"{titel}  ({artikel})";
            if (!string.IsNullOrWhiteSpace(afmeting))
                kop += $"  ·  {afmeting}";
            lines.Add(kop);

            lines.AddRange(afwerkingen);
            lines.AddRange(overig);
            if (!string.IsNullOrWhiteSpace(lijstOpm)) lines.Add($"Lijst: {lijstOpm}");
            if (!string.IsNullOrWhiteSpace(regelOpm)) lines.Add($"Opmerking: {regelOpm}");
            if (!string.IsNullOrWhiteSpace(afhaal))   lines.Add($"Afhalen: {afhaal}");

            return string.Join("\n", lines);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
