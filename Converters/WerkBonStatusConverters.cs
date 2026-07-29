using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QuadroApp.Model.DB;

namespace QuadroApp.Converters
{
    /// <summary>
    /// US-51 — kleurt een werkbon-statusbadge naar de fase in de werkplaats.
    /// Grijs = gepland, blauw = in uitvoering, amber = afgewerkt, groen = afgehaald.
    /// Zelfde palet als <see cref="OfferteStatusToBrushConverter"/> voor een consistente huisstijl.
    /// </summary>
    public sealed class WerkBonStatusToBrushConverter : IValueConverter
    {
        public static readonly WerkBonStatusToBrushConverter Instance = new();

        private static readonly IBrush Grijs = new SolidColorBrush(Color.Parse("#6B7280"));
        private static readonly IBrush Blauw = new SolidColorBrush(Color.Parse("#2A78D6"));
        private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#BA7517"));
        private static readonly IBrush Groen = new SolidColorBrush(Color.Parse("#1D9E75"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is WerkBonStatus s
                ? s switch
                {
                    WerkBonStatus.Gepland      => Grijs,
                    WerkBonStatus.InUitvoering => Blauw,
                    WerkBonStatus.Afgewerkt    => Amber,
                    WerkBonStatus.Afgehaald    => Groen,
                    _                          => Grijs
                }
                : Brushes.Transparent;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>US-51 — leesbaar NL-label voor een <see cref="BestelVorm"/> (In verstek / In lengte / Gemonteerd).</summary>
    public sealed class BestelVormToLabelConverter : IValueConverter
    {
        public static readonly BestelVormToLabelConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is BestelVorm v
                ? v switch
                {
                    BestelVorm.Verstek    => "In verstek",
                    BestelVorm.InLengte   => "In lengte",
                    BestelVorm.Gemonteerd => "Gemonteerd",
                    _                     => v.ToString()
                }
                : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>US-51 — leesbaar NL-label voor een <see cref="WerkBonStatus"/>.</summary>
    public sealed class WerkBonStatusToLabelConverter : IValueConverter
    {
        public static readonly WerkBonStatusToLabelConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is WerkBonStatus s
                ? s switch
                {
                    WerkBonStatus.Gepland      => "Gepland",
                    WerkBonStatus.InUitvoering => "In uitvoering",
                    WerkBonStatus.Afgewerkt    => "Afgewerkt",
                    WerkBonStatus.Afgehaald    => "Afgehaald",
                    _                          => s.ToString()
                }
                : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
