using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace QuadroApp.Converters
{
    /// <summary>
    /// Tekstvak ↔ <c>decimal?</c> waarbij een leeg vak <c>null</c> is (bv. de afgesproken prijs: leeg = de berekende
    /// prijs, 0 = echt gratis). Zonder deze converter mislukte de omzetting van "" en bleef de oude waarde staan.
    /// Komma en punt worden allebei aanvaard; andere tekst geeft de gewone foutmelding op het veld.
    /// </summary>
    public sealed class LeegIsNullDecimalConverter : IValueConverter
    {
        public static readonly LeegIsNullDecimalConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is decimal d ? d.ToString("0.##", culture) : "";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var tekst = (value as string)?.Trim().Replace("€", "").Trim();
            if (string.IsNullOrEmpty(tekst)) return null;
            if (ProbeerLezen(tekst, culture, out var d)) return d;
            return new BindingNotification(new FormatException(tekst), BindingErrorType.Error);
        }

        /// <summary>Leest "45,5", "45.5" of "1.234,50" (nl-BE); publiek voor de tests.</summary>
        public static bool ProbeerLezen(string tekst, CultureInfo culture, out decimal waarde)
        {
            var nl = culture.NumberFormat.NumberDecimalSeparator == "," ? culture : CultureInfo.GetCultureInfo("nl-BE");
            // Alleen een punt en geen komma: als decimaalteken lezen (45.5 → 45,5).
            if (tekst.Contains('.') && !tekst.Contains(','))
                return decimal.TryParse(tekst, NumberStyles.Number, CultureInfo.InvariantCulture, out waarde);
            return decimal.TryParse(tekst, NumberStyles.Number, nl, out waarde);
        }
    }
}
