using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace QuadroApp.Views;

/// <summary>
/// Een leeg getalveld (of tekst die geen getal is) gaf in het rood de technische melding
/// "InvalidCastException: Could not convert '(null)' to System.Int32". Avalonia zet die fout van de
/// binding zelf op het veld; hier wordt ze voor alle getalvelden en tekstvakken vervangen door een
/// duidelijke Nederlandse tekst. De waarde in het scherm-model blijft de vorige (er wordt niets bewaard).
/// Meldingen van de eigen validatie (gewone tekst) blijven ongewijzigd.
/// </summary>
public static class GetalInvoerFout
{
    public const string Tekst = "Ongeldige invoer: vul een getal in.";

    private static bool _geregistreerd;

    public static void Registreer(Application app)
    {
        if (_geregistreerd) return;
        _geregistreerd = true;
        Func<object?, object?> vertaal = Vertaal;
        foreach (var stijl in new[]
                 {
                     new Style(x => x.OfType<NumericUpDown>()),
                     new Style(x => x.OfType<TextBox>())
                 })
        {
            stijl.Setters.Add(new Setter(DataValidationErrors.ErrorConverterProperty, vertaal));
            app.Styles.Add(stijl);
        }
    }

    /// <summary>Fout van een mislukte omzetting (leeg, letters, te groot) → duidelijke tekst; de rest blijft.</summary>
    public static object? Vertaal(object? fout) => fout switch
    {
        InvalidCastException or FormatException or OverflowException => Tekst,
        Exception ex when ex.InnerException is InvalidCastException or FormatException or OverflowException => Tekst,
        _ => fout
    };
}
