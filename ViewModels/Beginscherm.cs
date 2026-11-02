using Avalonia.Media;
using QuadroApp.Model.DB;
using System;
using System.Globalization;

namespace QuadroApp.ViewModels
{
    /// <summary>US-105 — teksten van het beginscherm (los van de ViewModel, zodat ze testbaar zijn).</summary>
    public static class Beginscherm
    {
        private static readonly CultureInfo Nl = CultureInfo.GetCultureInfo("nl-BE");

        public static string Begroeting(DateTime nu) => nu.Hour switch
        {
            < 12 => "Goedemorgen",
            < 18 => "Goedemiddag",
            _ => "Goedenavond"
        };

        /// <summary>"Zaterdag 31 oktober 2026".</summary>
        public static string DatumTekst(DateTime dag)
        {
            var tekst = dag.ToString("dddd d MMMM yyyy", Nl);
            return char.ToUpper(tekst[0], Nl) + tekst[1..];
        }

        public static string Bedrag(decimal bedrag) => bedrag.ToString("€ #,##0.00", Nl);

        /// <summary>Hoe lang geleden: "5 min", "3 u", "2 d".</summary>
        public static string Geleden(DateTime sindsUtc, DateTime nuUtc)
        {
            var d = nuUtc - sindsUtc;
            if (d.TotalMinutes < 1) return "net";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours} u";
            return $"{(int)d.TotalDays} d";
        }
    }

    /// <summary>US-105 — één regel in "Voorraad &amp; leveringen".</summary>
    public sealed record WaarschuwingRegel(VoorraadAlertType Type, string Soort, string Bericht, string Wanneer)
    {
        public IBrush Kleur => new SolidColorBrush(Color.Parse(Type switch
        {
            VoorraadAlertType.LowStock or VoorraadAlertType.BelowMinimum => "#DC2626",
            VoorraadAlertType.OpenShortage => "#3B82F6",
            VoorraadAlertType.OrderOverdue => "#A855F7",
            _ => "#F97316"
        }));

        public static WaarschuwingRegel Van(VoorraadAlert a, DateTime nuUtc)
            => new(a.AlertType, a.AlertType.Label(), a.Bericht, Beginscherm.Geleden(a.AangemaaktOp, nuUtc));
    }
}
