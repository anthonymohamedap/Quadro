using Avalonia;
using Avalonia.Controls;

namespace QuadroApp.Views;

/// <summary>
/// Feedback Veerle (12/11, 9.1): een lege datumkiezer toonde de Engelse tekst "day", "month" en
/// "year". Avalonia zet die tekst zelf in de onderdelen van de datumkiezer; telkens dat gebeurt,
/// wordt ze hier meteen vervangen door "dag", "maand" en "jaar".
/// </summary>
public static class DatumKiezerTekst
{
    private static bool _geregistreerd;

    public static void Registreer()
    {
        if (_geregistreerd) return;
        _geregistreerd = true;
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((tekst, _) =>
        {
            if (tekst.TemplatedParent is not DatePicker) return;
            var vertaald = Vertaal(tekst.Text);
            if (vertaald != tekst.Text) tekst.Text = vertaald;
        });
    }

    public static string? Vertaal(string? tekst) => tekst switch
    {
        "day" => "dag",
        "month" => "maand",
        "year" => "jaar",
        _ => tekst
    };
}
