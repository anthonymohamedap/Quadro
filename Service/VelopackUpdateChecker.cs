using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace QuadroApp.Service;

/// <summary>
/// Silently checks GitHub Releases for a newer version, downloads it in the background
/// and shows a toast when ready. The update is applied on the next app restart.
/// Called once at startup from App.axaml.cs — never blocks the UI thread.
/// </summary>
internal static class VelopackUpdateChecker
{
    public static async Task CheckAndNotifyAsync(IServiceProvider services, ILogger logger)
    {
        try
        {
            // ── Update-bron ────────────────────────────────────────────────────────────
            // Productie : GitHub Releases (standaard).
            // Lokale test: definieer LOCAL_TEST bij het bouwen — test-velopack-local.ps1
            //              doet dit automatisch zodat de app http://localhost:8080 gebruikt.
            // Terugzetten: git checkout App.axaml.cs
#if LOCAL_TEST
            const string repoUrl = "http://localhost:8080";
#else
            const string repoUrl = "https://github.com/anthonymohamedap/Quadro";
#endif

            var source = new GithubSource(repoUrl, accessToken: null, prerelease: false);
            var channel = OperatingSystem.IsMacOS() ? "osx" : "win";
            var mgr = new UpdateManager(source, new UpdateOptions { ExplicitChannel = channel });

            // Not running as a Velopack-installed app (e.g. dev machine) → skip silently.
            if (!mgr.IsInstalled) return;

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null) return;

            logger.LogInformation("[Update] Nieuwe versie beschikbaar: {Version}", newVersion.TargetFullRelease.Version);

            await mgr.DownloadUpdatesAsync(newVersion);

            logger.LogInformation("[Update] Download klaar. Wordt toegepast bij volgende herstart.");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var toast = services.GetService<IToastService>();
                toast?.Info("🔄 Update gedownload.", "Herstart nu", () => mgr.ApplyUpdatesAndRestart(newVersion));
            });
        }
        catch (System.Net.Http.HttpRequestException httpEx)
        {
            // Netwerk niet beschikbaar, DNS-fout, 404, … — allemaal niet-kritiek.
            logger.LogInformation("[Update] Update-controle overgeslagen (netwerk): {Message}", httpEx.Message);
        }
        catch (Exception ex)
        {
            // Update check should never crash the app — only log, never write to crash.log.
            logger.LogWarning(ex, "[Update] Update-controle mislukt (niet kritiek): {Message}", ex.Message);
        }
    }
}
