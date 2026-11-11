using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace QuadroApp.Views;

/// <summary>
/// Feedback Veerle: vensters met een vaste hoogte (bv. de werkbon, 760) vielen op een laptopscherm
/// onderaan buiten beeld, waardoor je niet tot de laatste knop ("Bestelling plaatsen") kon scrollen.
/// Elk venster wordt bij het openen begrensd tot het werkgebied van zijn scherm en, waar nodig,
/// opnieuw binnen dat scherm gezet.
/// </summary>
public static class VensterGrootte
{
    private static bool _geregistreerd;

    public static void Registreer()
    {
        if (_geregistreerd) return;
        _geregistreerd = true;
        Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => PasAanScherm(w), RoutingStrategies.Direct);
    }

    public static void PasAanScherm(Window venster)
    {
        if (venster.WindowState != WindowState.Normal) return;
        var scherm = venster.Screens.ScreenFromWindow(venster) ?? venster.Screens.Primary;
        if (scherm is null) return;

        var schaal = scherm.Scaling > 0 ? scherm.Scaling : 1d;
        var werk = scherm.WorkingArea;
        // Ruimte voor de titelbalk van het systeem.
        var maxHoogte = Math.Max(300, werk.Height / schaal - 40);
        var maxBreedte = Math.Max(400, werk.Width / schaal - 20);

        var aangepast = false;
        if (venster.MinHeight > maxHoogte) venster.MinHeight = maxHoogte;
        if (venster.MinWidth > maxBreedte) venster.MinWidth = maxBreedte;
        if (Hoogte(venster) > maxHoogte) { venster.Height = maxHoogte; aangepast = true; }
        if (Breedte(venster) > maxBreedte) { venster.Width = maxBreedte; aangepast = true; }
        if (!aangepast) return;

        // Terug binnen het scherm zetten (bovenaan het werkgebied als het niet meer past).
        var pos = venster.Position;
        var hoogtePx = (int)(Hoogte(venster) * schaal);
        var breedtePx = (int)(Breedte(venster) * schaal);
        var x = Math.Clamp(pos.X, werk.X, Math.Max(werk.X, werk.Right - breedtePx));
        var y = Math.Clamp(pos.Y, werk.Y, Math.Max(werk.Y, werk.Bottom - hoogtePx - (int)(40 * schaal)));
        venster.Position = new PixelPoint(x, y);
    }

    private static double Hoogte(Window w) => double.IsNaN(w.Height) ? w.Bounds.Height : w.Height;
    private static double Breedte(Window w) => double.IsNaN(w.Width) ? w.Bounds.Width : w.Width;
}
