using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace QuadroApp.Converters
{
    /// <summary>
    /// REL-01: toont een in UTC opgeslagen gebeurtenis-tijdstip in lokale tijd.
    /// Gebruik ConverterParameter voor het datumformaat, bv. 'dd-MM-yyyy HH:mm'.
    /// Nullable/lege datums geven een lege string.
    /// </summary>
    public class UtcToLocalConverter : IValueConverter
    {
        public static readonly UtcToLocalConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DateTime dt) return string.Empty;
            if (dt == default) return string.Empty;

            // In de DB opgeslagen als UTC; markeer expliciet en converteer naar lokaal.
            var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            var local = utc.ToLocalTime();

            var format = parameter as string ?? "dd-MM-yyyy HH:mm";
            return local.ToString(format, culture);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
