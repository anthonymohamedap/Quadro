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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
