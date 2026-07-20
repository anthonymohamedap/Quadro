// QuadroApp/Converters/BoolToThicknessConverter.cs
using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    public sealed class BoolToThicknessConverter : IValueConverter
    {
        public Thickness WhenTrue { get; set; } = new Thickness(0);
        public Thickness WhenFalse { get; set; } = new Thickness(520, 0, -520, 0); // slide out to right

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? WhenTrue : WhenFalse;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

