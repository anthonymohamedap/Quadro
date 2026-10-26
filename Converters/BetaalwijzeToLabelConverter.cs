using System;
using System.Globalization;
using Avalonia.Data.Converters;
using QuadroApp.Model.DB;

namespace QuadroApp.Converters
{
    /// <summary>US-79 — leesbaar label voor een <see cref="Betaalwijze"/> ("Credit card" i.p.v. "CreditCard").</summary>
    public sealed class BetaalwijzeToLabelConverter : IValueConverter
    {
        public static readonly BetaalwijzeToLabelConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Betaalwijze b ? b.Label() : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
