using System;
using System.Globalization;
using Avalonia.Data.Converters;
using QuadroApp.Model.DB;

namespace QuadroApp.Converters
{
    /// <summary>US-85 — toont een status in gewone woorden (zie <see cref="StatusLabels"/>).
    /// Werkt voor offerte-, werkbon-, bestelbon-, voorraadmelding- en leveranciersbestelling-statussen.</summary>
    public sealed class StatusLabelConverter : IValueConverter
    {
        public static readonly StatusLabelConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var label = value switch
            {
                OfferteStatus s               => s.Label(),
                WerkBonStatus s               => s.Label(),
                FactuurStatus s               => s.Label(),
                VoorraadAlertType t           => t.Label(),
                LeverancierBestellingStatus s => s.Label(),
                null                          => "",
                _                             => value.ToString() ?? ""
            };
            return parameter is string format ? string.Format(culture, format, label) : label;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
