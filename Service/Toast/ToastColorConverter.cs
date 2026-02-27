using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
namespace QuadroApp.Service.Toast
{


    public class ToastColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                ToastType.Success => new SolidColorBrush(Color.Parse("#52c41a")),
                ToastType.Error => new SolidColorBrush(Color.Parse("#ff4d4f")),
                ToastType.Warning => new SolidColorBrush(Color.Parse("#faad14")),
                ToastType.Info => new SolidColorBrush(Color.Parse("#1677ff")),
                _ => new SolidColorBrush(Color.Parse("#1677ff")),
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
