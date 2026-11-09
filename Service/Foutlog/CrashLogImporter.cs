using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;

namespace QuadroApp.Service.Foutlog;

/// <summary>
/// Leest crash.log in de FoutLogs-tabel, zodat ook fouten van vóór de database-logging (of toen de
/// database onbereikbaar was) zichtbaar worden. crash.log zelf wordt nooit gewijzigd of gewist.
/// </summary>
public static class CrashLogImporter
{
    private const int BewaarDagen = 180;
    private static readonly Regex Header = new(@"^\[(?<ts>[^\]]+)\]\s?(?<rest>.*)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex TypeNaam = new(@"^[A-Za-z_][\w.`+<>\[\],]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly CultureInfo NlBe = SafeCulture("nl-BE");
    private static readonly CultureInfo EnUs = SafeCulture("en-US");

    private static CultureInfo SafeCulture(string naam)
    {
        try { return CultureInfo.GetCultureInfo(naam); } catch { return CultureInfo.InvariantCulture; }
    }

    // ───────────────────────── Parsen ─────────────────────────

    /// <summary>Zet de tekst van crash.log om in FoutLog-rijen (nog niet opgeslagen). Faalt nooit.</summary>
    public static IReadOnlyList<FoutLog> Parse(string tekst, string computer)
    {
        var resultaat = new List<FoutLog>();
        if (string.IsNullOrWhiteSpace(tekst)) return resultaat;

        try
        {
            var regels = tekst.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            DateTime? tijd = null;
            var rest = "";
            var inhoud = new List<string>();

            void Afsluiten()
            {
                if (tijd is not null)
                {
                    try
                    {
                        var log = MaakLog(tijd.Value, rest, inhoud, computer);
                        if (log is not null) resultaat.Add(log);
                    }
                    catch { /* een kapot blok mag de rest niet tegenhouden */ }
                }
                tijd = null;
                rest = "";
                inhoud.Clear();
            }

            foreach (var regel in regels)
            {
                if (TryParseHeader(regel, out var utc, out var r))
                {
                    Afsluiten();
                    tijd = utc;
                    rest = r;
                }
                else if (regel.Length > 0 && regel[0] == '[' && Header.IsMatch(regel))
                {
                    // [Config] / [DB] / [DEV] ...: eindigt het blok, wordt genegeerd.
                    Afsluiten();
                }
                else if (tijd is not null)
                {
                    inhoud.Add(regel);
                }
            }
            Afsluiten();
        }
        catch { /* nooit gooien */ }

        return resultaat;
    }

    private static FoutLog? MaakLog(DateTime utc, string rest, List<string> inhoud, string computer)
    {
        var bron = FoutLogBron.Geimporteerd;
        string? scherm = null;
        var acties = new List<string>();
        string? hash = null;
        List<string> uitzondering;

        if (rest.StartsWith("Bron: ", StringComparison.Ordinal))
        {
            var origineel = rest["Bron: ".Length..].Trim();
            // Enkel de broncode ("UI-thread"), niet de toelichting tussen haakjes uit crash.log.
            var haakje = origineel.IndexOf(" (", StringComparison.Ordinal);
            if (haakje > 0) origineel = origineel[..haakje];
            if (origineel.Length > 0) bron = $"{FoutLogBron.Geimporteerd} ({origineel})";

            var i = 0;
            if (i < inhoud.Count && inhoud[i].StartsWith("Scherm: ", StringComparison.Ordinal))
                scherm = inhoud[i++]["Scherm: ".Length..].Trim();
            if (i < inhoud.Count && inhoud[i].StartsWith("Laatste acties:", StringComparison.Ordinal))
            {
                i++;
                while (i < inhoud.Count && inhoud[i].StartsWith("  [", StringComparison.Ordinal))
                    acties.Add(inhoud[i++].Trim());
            }
            if (i < inhoud.Count && inhoud[i].StartsWith("Hash: ", StringComparison.Ordinal))
            {
                var h = inhoud[i++]["Hash: ".Length..].Trim();
                if (h.Length == 64) hash = h.ToLowerInvariant();
            }
            uitzondering = inhoud.Skip(i).ToList();
        }
        else
        {
            uitzondering = new List<string>();
            if (!string.IsNullOrWhiteSpace(rest)) uitzondering.Add(rest);
            uitzondering.AddRange(inhoud);
        }

        while (uitzondering.Count > 0 && string.IsNullOrWhiteSpace(uitzondering[0])) uitzondering.RemoveAt(0);
        while (uitzondering.Count > 0 && string.IsNullOrWhiteSpace(uitzondering[^1])) uitzondering.RemoveAt(uitzondering.Count - 1);
        if (uitzondering.Count == 0) return null;   // halfblok: header zonder exception

        var eerste = uitzondering[0].Trim();
        string type, melding;
        var sep = eerste.IndexOf(": ", StringComparison.Ordinal);
        var kandidaat = sep > 0 ? eerste[..sep] : eerste;
        if (TypeNaam.IsMatch(kandidaat))
        {
            type = kandidaat;
            melding = sep > 0 ? eerste[(sep + 2)..] : "";
        }
        else
        {
            type = "Onbekend";
            melding = eerste;
        }

        var ruw = string.Join("\n", uitzondering);
        hash ??= FoutLogHash.Bereken(utc, type, ruw, computer);

        return new FoutLog
        {
            Tijdstip = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Bron = FoutLogFilter.Kort(bron, FoutLogFilter.MaxBron),
            ExceptionType = FoutLogFilter.FilterKort(type, FoutLogFilter.MaxExceptionType),
            Melding = FoutLogFilter.FilterKort(melding, FoutLogFilter.MaxMelding),
            Stacktrace = FoutLogFilter.FilterKort(ruw, FoutLogFilter.MaxStacktrace),
            Scherm = string.IsNullOrWhiteSpace(scherm) ? null : FoutLogFilter.FilterKort(scherm, FoutLogFilter.MaxScherm),
            LaatsteKlikken = acties.Count == 0 ? null : FoutLogFilter.FilterKort(string.Join("\n", acties), FoutLogFilter.MaxLaatsteKlikken),
            Computernaam = FoutLogFilter.Kort(computer, FoutLogFilter.MaxComputernaam),
            Hash = hash,
        };
    }

    /// <summary>Is dit een blokkop <c>[datum] rest</c> met een echte datum? Levert het tijdstip in UTC.</summary>
    private static bool TryParseHeader(string regel, out DateTime utc, out string rest)
    {
        utc = default;
        rest = "";
        if (regel.Length < 10 || regel[0] != '[') return false;
        var m = Header.Match(regel);
        if (!m.Success) return false;
        var ts = m.Groups["ts"].Value.Trim();
        if (ts.Length < 8 || !char.IsDigit(ts[0]) || !TryParseTijd(ts, out var lokaal)) return false;
        utc = lokaal.ToUniversalTime();
        rest = m.Groups["rest"].Value;
        return true;
    }

    private static bool TryParseTijd(string ts, out DateTime lokaal)
    {
        const DateTimeStyles stijl = DateTimeStyles.None;
        var ok = DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, stijl, out var d)
              || DateTime.TryParse(ts, CultureInfo.CurrentCulture, stijl, out d)
              || DateTime.TryParse(ts, NlBe, stijl, out d)
              || DateTime.TryParse(ts, EnUs, stijl, out d)
              || DateTime.TryParse(ts, CultureInfo.InvariantCulture, stijl, out d);
        lokaal = ok ? new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Local) : default;
        return ok;
    }

    // ───────────────────────── Bestand ─────────────────────────

    /// <summary>Leest het einde van crash.log (laatste <paramref name="maxBytes"/>), vanaf de eerste volledige blokkop.</summary>
    public static string LeesStaart(string pad, long maxBytes = 2 * 1024 * 1024)
    {
        try
        {
            if (!File.Exists(pad)) return "";
            using var fs = new FileStream(pad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = Math.Max(0, fs.Length - maxBytes);
            fs.Seek(start, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            var tekst = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            if (start == 0) return tekst;

            // Midden in het bestand begonnen: sla alles voor de eerste echte blokkop over.
            var pos = 0;
            while (pos < tekst.Length)
            {
                var eind = tekst.IndexOf('\n', pos);
                var lijn = (eind < 0 ? tekst[pos..] : tekst[pos..eind]).TrimEnd('\r');
                if (TryParseHeader(lijn, out _, out _)) return tekst[pos..];
                if (eind < 0) break;
                pos = eind + 1;
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    // ───────────────────────── Database ─────────────────────────

    /// <summary>
    /// Importeert nieuwe blokken uit crash.log (niet ouder dan 180 dagen). Geeft het aantal toegevoegde rijen terug.
    /// </summary>
    public static async Task<int> ImporteerAsync(
        IDbContextFactory<AppDbContext> factory, string pad, string computer, CancellationToken ct, DateTime? nuUtc = null)
    {
        var grens = (nuUtc ?? DateTime.UtcNow).AddDays(-BewaarDagen);
        var kandidaten = Parse(LeesStaart(pad), computer)
            .Where(l => l.Tijdstip >= grens)
            .GroupBy(l => l.Hash)
            .Select(g => g.First())
            .ToList();
        if (kandidaten.Count == 0) return 0;

        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var bestaand = new HashSet<string>();
        foreach (var deel in kandidaten.Select(k => k.Hash).Chunk(200))
        {
            var gevonden = await db.FoutLogs.AsNoTracking()
                .Where(f => deel.Contains(f.Hash))
                .Select(f => f.Hash)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var h in gevonden) bestaand.Add(h);
        }

        var nieuw = kandidaten.Where(k => !bestaand.Contains(k.Hash)).ToList();
        if (nieuw.Count == 0) return 0;

        db.FoutLogs.AddRange(nieuw);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return nieuw.Count;
        }
        catch (DbUpdateException)
        {
            // Een dubbele hash (andere pc was ons voor): één voor één, dubbels negeren.
            db.ChangeTracker.Clear();
            var toegevoegd = 0;
            foreach (var log in nieuw)
            {
                ct.ThrowIfCancellationRequested();
                db.FoutLogs.Add(log);
                try
                {
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    toegevoegd++;
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                }
            }
            return toegevoegd;
        }
    }

    /// <summary>Verwijdert bekeken fouten ouder dan 180 dagen. Geeft het aantal verwijderde rijen terug.</summary>
    public static async Task<int> RuimOpAsync(IDbContextFactory<AppDbContext> factory, DateTime nuUtc, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var grens = nuUtc.AddDays(-BewaarDagen);
        return await db.FoutLogs.Where(f => f.IsBekeken && f.Tijdstip < grens).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
