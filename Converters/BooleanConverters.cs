using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace QuadroApp.Converters
{
    public static class BooleanConverters
    {
        // ✅ Controleer of iets niet null is (voor IsEnabled)
        public static readonly IValueConverter IsNotNull =
            new FuncValueConverter<object?, bool>(value => value is not null);

        // ✅ Geeft kleur terug bij selectie: eerste parameter = TrueColor|FalseColor
        public static readonly IValueConverter ToBrush =
            new FuncValueConverter<object?, IBrush?>(value =>
            {
                if (value is string s && s.Contains("|"))
                {
                    var parts = s.Split('|');
                    return new SolidColorBrush(Color.Parse(parts[0]));
                }
                return value is bool b && b
                    ? new SolidColorBrush(Color.Parse("#F5C242")) // geselecteerd
                    : new SolidColorBrush(Color.Parse("White"));  // niet geselecteerd
            });
    }
}
