using Avalonia.Data.Converters;
using Avalonia.Media;
using QuadroApp.Model.DB;

namespace QuadroApp.Converters
{
    public static class BooleanConverters
    {
        // ✅ Kleur per bestelstatus (voor een statusbadge in de leverancier-bestellingen).
        public static readonly IValueConverter BestelStatusToBrush =
            new FuncValueConverter<LeverancierBestellingStatus, IBrush>(status => status switch
            {
                LeverancierBestellingStatus.Besteld           => new SolidColorBrush(Color.Parse("#2563EB")), // blauw
                LeverancierBestellingStatus.DeelsOntvangen    => new SolidColorBrush(Color.Parse("#D97706")), // amber
                LeverancierBestellingStatus.VolledigOntvangen => new SolidColorBrush(Color.Parse("#16A34A")), // groen
                LeverancierBestellingStatus.Geannuleerd       => new SolidColorBrush(Color.Parse("#DC2626")), // rood
                _                                              => new SolidColorBrush(Color.Parse("#6B7280")), // grijs (Concept)
            });

        // ✅ Controleer of iets niet null is (voor IsEnabled)
        public static readonly IValueConverter IsNotNull =
            new FuncValueConverter<object?, bool>(value => value is not null);

        // ✅ True als een decimaal groter dan 0 is (bv. om een kortingregel te tonen)
        public static readonly IValueConverter IsPositive =
            new FuncValueConverter<decimal, bool>(value => value > 0m);

        // ✅ True als een count groter dan 0 is (bv. om een lijst/keuzelijst te tonen)
        public static readonly IValueConverter IsNotEmpty =
            new FuncValueConverter<int, bool>(count => count > 0);

        // ✅ Hex-string ("#345353") → penseel voor een kleurstaal; ongeldig/leeg = transparant
        public static readonly IValueConverter HexToBrush =
            new FuncValueConverter<string?, IBrush>(hex =>
            {
                if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
                var h = hex.Trim();
                if (!h.StartsWith("#")) h = "#" + h;
                try { return new SolidColorBrush(Color.Parse(h)); }
                catch { return Brushes.Transparent; }
            });

        // ✅ Geeft kleur terug bij selectie: eerste parameter = TrueColor|FalseColor
        public static readonly IValueConverter ToBrush =
            new FuncValueConverter<object?, IBrush?>(value =>
            {
                if (value is string s && s.Contains("|"))
                {
                    var parts = s.Split('|');
                    return new SolidColorBrush(Color.Parse(parts[0]));
                }
                return value is bool b && b
                    ? new SolidColorBrush(Color.Parse("#F5C242")) // geselecteerd
                    : new SolidColorBrush(Color.Parse("White"));  // niet geselecteerd
            });
    }
}
