using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    /// <summary>Bedrag ≠ 0 → Danger-brush uit QuadroTheme (rood); anders de standaard (geërfde) tekstkleur.</summary>
    public sealed class NietNulNaarDangerBrushConverter : IValueConverter
    {
        public static readonly NietNulNaarDangerBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var nietNul = value switch
            {
                decimal d => d != 0m,
                double d => d != 0d,
                _ => false
            };
            if (!nietNul || Application.Current is null)
                return AvaloniaProperty.UnsetValue;

            return Application.Current.TryFindResource("Danger", Application.Current.ActualThemeVariant, out var brush)
                ? brush
                : AvaloniaProperty.UnsetValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
