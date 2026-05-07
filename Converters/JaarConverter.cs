using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    /// <summary>Toont 0 als "Alle jaren" en andere integers als het jaar.</summary>
    public class JaarConverter : IValueConverter
    {
        public static readonly JaarConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is int jaar && jaar > 0 ? jaar.ToString() : "Alle jaren";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string s && int.TryParse(s, out var j) ? j : 0;
    }
}
