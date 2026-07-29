using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QuadroApp.Model.DB;

namespace QuadroApp.Converters
{
    /// <summary>
    /// US-46 — kleurt een offerte-statusbadge naar de fase in de levenscyclus.
    /// Grijs = nog niet in productie, blauw = onderweg, amber = afgewerkt,
    /// groen = gefactureerd/betaald, rood = geannuleerd.
    /// </summary>
    public sealed class OfferteStatusToBrushConverter : IValueConverter
    {
        public static readonly OfferteStatusToBrushConverter Instance = new();

        private static readonly IBrush Grijs  = new SolidColorBrush(Color.Parse("#6B7280"));
        private static readonly IBrush Blauw  = new SolidColorBrush(Color.Parse("#2A78D6"));
        private static readonly IBrush Amber  = new SolidColorBrush(Color.Parse("#BA7517"));
        private static readonly IBrush Groen  = new SolidColorBrush(Color.Parse("#1D9E75"));
        private static readonly IBrush Rood   = new SolidColorBrush(Color.Parse("#D03B3B"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is OfferteStatus s
                ? s switch
                {
                    OfferteStatus.Concept or OfferteStatus.Verzonden       => Grijs,
                    OfferteStatus.Goedgekeurd or OfferteStatus.InProductie => Blauw,
                    OfferteStatus.Afgewerkt                                => Amber,
                    OfferteStatus.Besteld or OfferteStatus.Betaald    => Groen,
                    OfferteStatus.Geannuleerd                              => Rood,
                    _                                                      => Grijs
                }
                : Brushes.Transparent;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
