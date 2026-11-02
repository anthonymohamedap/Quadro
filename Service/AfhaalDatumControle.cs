using QuadroApp.Model.DB;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuadroApp.Service
{
    /// <summary>
    /// US-81 — waarschuwt (niet blokkerend) als een afhaaldatum vóór de besteldatum van de bestelbon ligt.
    /// Kijkt naar de afhaaldatum van de bon zelf én naar die per inlijsting (tag <c>afhaal:yyyy-MM-dd</c>
    /// in de lijnomschrijving, US-27).
    /// </summary>
    public static class AfhaalDatumControle
    {
        private static readonly Regex AfhaalTag = new(@"afhaal:(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

        public static DateTime? VroegsteAfhaalDatum(Bestelbon bestelbon)
        {
            var perLijn = (bestelbon.Lijnen ?? Enumerable.Empty<BestelbonLijn>())
                .Select(l => AfhaalTag.Match(l.Omschrijving ?? string.Empty))
                .Where(m => m.Success)
                .Select(m => DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                .Where(d => d.HasValue);

            var alle = perLijn.Append(bestelbon.AfhaalDatum?.Date).Where(d => d.HasValue).ToList();
            return alle.Count == 0 ? null : alle.Min();
        }

        public static string? Waarschuwing(DateTime besteldatum, DateTime? afhaaldatum)
        {
            if (afhaaldatum is null || afhaaldatum.Value.Date >= besteldatum.Date)
                return null;

            return $"Let op: de afhaaldatum ({afhaaldatum.Value:dd/MM/yyyy}) ligt vóór de besteldatum ({besteldatum:dd/MM/yyyy}).";
        }
    }
}
