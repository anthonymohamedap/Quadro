using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace QuadroApp.Converters
{
    public class IntToBoolInverseConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0; // zichtbaar als er GEEN klanten zijn
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
