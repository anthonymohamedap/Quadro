using System;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;

namespace QuadroApp.Service.Foutlog;

/// <summary>
/// Registreert een crash: eerst synchroon in crash.log (lokaal, ruwe tekst), daarna in de database
/// (gefilterd). De databasekant faalt of blokkeert nooit: het teruggegeven <see cref="Task"/> faalt niet
/// en een time-out (standaard 5 s) stopt ook de retry-strategie van Npgsql.
/// Werkt vóór DI bestaat: de factory wordt pas bij gebruik opgevraagd.
/// </summary>
public sealed class FoutRegistratie
{
    private readonly string _crashLogPad;
    private readonly Func<IDbContextFactory<AppDbContext>?> _factory;
    private readonly Func<string?> _gebruiker;
    private readonly string _computer;
    private readonly string? _appVersie;
    private readonly TimeSpan _dbTimeout;

    // Stormbeveiliging: dezelfde fout hooguit 1x per minuut naar de DB, en nooit twee DB-schrijfacties tegelijk.
    // Overgeslagen fouten staan wel in crash.log en worden bij de volgende start geïmporteerd.
    private static readonly long HerhaalVensterMs = 60_000;
    private const int MaxOnthouden = 200;
    private readonly ConcurrentDictionary<string, long> _recent = new();
    private readonly SemaphoreSlim _dbSlot = new(1, 1);

    public FoutRegistratie(
        string crashLogPad,
        Func<IDbContextFactory<AppDbContext>?> factory,
        Func<string?> gebruiker,
        string computer,
        string? appVersie,
        TimeSpan? dbTimeout = null)
    {
        _crashLogPad = crashLogPad;
        _factory = factory;
        _gebruiker = gebruiker;
        _computer = computer;
        _appVersie = appVersie;
        _dbTimeout = dbTimeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Schrijf de fout naar crash.log (synchroon) en daarna naar de database (op de achtergrond).
    /// </summary>
    /// <param name="bron">Code uit <see cref="FoutLogBron"/>; komt in de database.</param>
    /// <param name="toelichting">Extra uitleg, enkel voor crash.log.</param>
    public Task Registreer(Exception ex, string bron, DateTime? nuLokaal = null, string? toelichting = null)
    {
        FoutLog? log = null;
        try
        {
            var lokaal = nuLokaal ?? DateTime.Now;
            lokaal = new DateTime(lokaal.Ticks - lokaal.Ticks % TimeSpan.TicksPerSecond, lokaal.Kind);

            var ruw = ex.ToString();
            var type = ex.GetType().FullName ?? ex.GetType().Name;
            var utc = lokaal.ToUniversalTime();
            var hash = FoutLogHash.Bereken(utc, type, ruw, _computer);

            // 1. Altijd eerst het bestand.
            SchrijfNaarBestand(ex, ruw, bron, toelichting, lokaal, hash);

            // 2. Het databaserecord bouwen (kan niet falen, maar blijft binnen de try).
            log = Bouw(ex, ruw, type, bron, utc, hash);
        }
        catch
        {
            return Task.CompletedTask;
        }

        if (!_dbSlot.Wait(0)) return Task.CompletedTask;   // al een schrijfactie bezig
        if (!MagProberen(log.Hash))
        {
            _dbSlot.Release();
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try { await SchrijfNaarDbAsync(log).ConfigureAwait(false); }
            finally { _dbSlot.Release(); }
        });
    }

    /// <summary>False als dezelfde hash de afgelopen minuut al naar de DB geprobeerd is. Thread-safe en begrensd.</summary>
    private bool MagProberen(string hash)
    {
        var nu = Environment.TickCount64;
        if (_recent.Count > MaxOnthouden)
        {
            foreach (var kv in _recent)
                if (nu - kv.Value > HerhaalVensterMs) _recent.TryRemove(kv.Key, out _);
            if (_recent.Count > MaxOnthouden) _recent.Clear();
        }

        var toegelaten = true;
        _recent.AddOrUpdate(hash, nu, (_, vorige) =>
        {
            if (nu - vorige <= HerhaalVensterMs) { toegelaten = false; return vorige; }
            return nu;
        });
        return toegelaten;
    }

    private void SchrijfNaarBestand(Exception ex, string ruw, string bron, string? toelichting, DateTime lokaal, string hash)
    {
        try
        {
            var bronTekst = string.IsNullOrWhiteSpace(toelichting) ? bron : $"{bron} ({toelichting})";
            var blok = $"[{lokaal.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}] " +
                       $"{CrashContext.Beschrijf(bronTekst)}\nHash: {hash}\n{ruw}\n\n";
            File.AppendAllText(_crashLogPad, blok);
        }
        catch { /* logging mag de app nooit laten vallen */ }
    }

    private FoutLog Bouw(Exception ex, string ruw, string type, string bron, DateTime utc, string hash)
    {
        var melding = ex is AggregateException or TargetInvocationException && ex.InnerException is not null
            ? ex.GetBaseException().Message
            : ex.Message;

        var (scherm, acties) = CrashContext.Momentopname();
        string? gebruiker = null;
        try { gebruiker = _gebruiker(); } catch { /* onbekend */ }

        return new FoutLog
        {
            Tijdstip = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Bron = FoutLogFilter.Kort(bron, FoutLogFilter.MaxBron),
            ExceptionType = FoutLogFilter.Kort(type, FoutLogFilter.MaxExceptionType),
            Melding = FoutLogFilter.FilterKort(melding, FoutLogFilter.MaxMelding),
            Stacktrace = FoutLogFilter.FilterKort(ruw, FoutLogFilter.MaxStacktrace),
            Scherm = scherm is null ? null : FoutLogFilter.FilterKort(scherm, FoutLogFilter.MaxScherm),
            LaatsteKlikken = acties.Count == 0
                ? null
                : FoutLogFilter.FilterKort(string.Join("\n", acties), FoutLogFilter.MaxLaatsteKlikken),
            AppVersie = FoutLogFilter.Kort(_appVersie, FoutLogFilter.MaxAppVersie),
            Computernaam = FoutLogFilter.Kort(_computer, FoutLogFilter.MaxComputernaam),
            Gebruiker = FoutLogFilter.Kort(gebruiker, FoutLogFilter.MaxGebruiker) is { Length: > 0 } g ? g : null,
            Hash = hash,
        };
    }

    private async Task SchrijfNaarDbAsync(FoutLog log)
    {
        try
        {
            var factory = _factory();
            if (factory is null) return;

            using var cts = new CancellationTokenSource(_dbTimeout);
            await using var db = await factory.CreateDbContextAsync(cts.Token).ConfigureAwait(false);

            if (await db.FoutLogs.AsNoTracking().AnyAsync(f => f.Hash == log.Hash, cts.Token).ConfigureAwait(false))
                return;

            db.FoutLogs.Add(log);
            try { await db.SaveChangesAsync(cts.Token).ConfigureAwait(false); }
            catch (DbUpdateException) { /* zelfde fout tegelijk geschreven: unieke hash, geen probleem */ }
        }
        catch
        {
            // Database niet bereikbaar, tabel nog niet gemigreerd, time-out...: crash.log heeft de fout al.
        }
    }

    /// <summary>Versie van de app (InformationalVersion zonder "+commit", anders Version).</summary>
    public static string? HuidigeAppVersie()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus >= 0 ? info[..plus] : info;
            }
            return asm.GetName().Version?.ToString();
        }
        catch { return null; }
    }
}
