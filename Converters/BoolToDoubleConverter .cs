// QuadroApp/Converters/BoolToDoubleConverter.cs
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    public sealed class BoolToDoubleConverter : IValueConverter
    {
        public double WhenTrue { get; set; } = 1.0;
        public double WhenFalse { get; set; } = 0.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? WhenTrue : WhenFalse;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}