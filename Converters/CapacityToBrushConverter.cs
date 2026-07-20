using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    public class CapacityToBrushConverter : IValueConverter
    {
        public object? Convert(object? value,
                               Type targetType,
                               object? parameter,
                               CultureInfo culture)
        {
            if (value is double ratio)
            {
                if (ratio >= 1.0) return Brushes.Red;
                if (ratio >= 0.75) return Brushes.Orange;
                if (ratio >= 0.5) return Brushes.Yellow;
                if (ratio > 0) return Brushes.LightGreen;
            }

            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value,
                                   Type targetType,
                                   object? parameter,
                                   CultureInfo culture)
        {
            // We gebruiken geen two-way binding, dus niet nodig
            throw new NotSupportedException();
        }
    }
}