using Avalonia;
using System;
using Velopack;

namespace QuadroApp;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // ⚠️ Velopack MUST be the very first call — it handles installer/uninstaller
        // hooks and exits early when running as an update hook. Nothing else runs first.
        VelopackApp.Build().Run();

        // PostgreSQL/Npgsql: zet 'legacy timestamp behavior' aan VOORDAT er ook maar
        // één Npgsql-verbinding wordt opgebouwd (moet dus hier, vóór BuildAvaloniaApp).
        // Zonder dit koppelt Npgsql DateTime aan 'timestamp with time zone' en eist het
        // Kind=Utc. De app slaat echter kalenderdatums bewust LOKAAL op (offertedatum,
        // planning, factuurdatums = Kind=Local/Unspecified) naast UTC-gebeurtenis-
        // tijdstippen (REL-01). Dat mengt niet op een timestamptz-kolom -> crash
        // ("Cannot write DateTime with Kind=Local/Unspecified ... only UTC is supported").
        // Met deze switch worden DateTime-kolommen 'timestamp without time zone' (wall-clock,
        // geen tz-conversie) — exact zoals SQLite het opslaat. UtcToLocalConverter blijft
        // kloppen. Heeft geen effect op SQLite-installaties.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
