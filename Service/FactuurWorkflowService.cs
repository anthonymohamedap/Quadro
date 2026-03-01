using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class FactuurWorkflowService : IFactuurWorkflowService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public FactuurWorkflowService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var existing = await db.Facturen
            .Include(x => x.Lijnen)
            .FirstOrDefaultAsync(x => x.WerkBonId == werkBonId);
        if (existing is not null)
            return existing;

        var werkbon = await db.WerkBonnen
            .Include(w => w.Offerte)
                .ThenInclude(o => o!.Klant)
            .Include(w => w.Offerte)
                .ThenInclude(o => o!.Regels)
                    .ThenInclude(r => r.TypeLijst)
            .FirstOrDefaultAsync(w => w.Id == werkBonId);

        if (werkbon is null)
            throw new InvalidOperationException("Werkbon niet gevonden.");

        if (werkbon.Status != WerkBonStatus.Afgewerkt)
            throw new InvalidOperationException("Factuur kan enkel gemaakt worden voor afgewerkte werkbonnen.");

        var offerte = werkbon.Offerte ?? throw new InvalidOperationException("Werkbon heeft geen offerte.");
        var klant = offerte.Klant;

        var now = DateTime.Today;
        var jaar = now.Year;
        var volgNr = (await db.Facturen.Where(f => f.Jaar == jaar).MaxAsync(f => (int?)f.VolgNr) ?? 0) + 1;

        var btwPct = await LeesBtwPctAsync(db);
        var vrijgesteld = await IsBtwVrijgesteldAsync(db);

        var factuur = new Factuur
        {
            WerkBonId = werkBonId,
            Jaar = jaar,
            VolgNr = volgNr,
            FactuurNummer = $"{jaar}-{volgNr}",
            KlantNaam = BuildKlantNaam(klant),
            KlantAdres = BuildAdres(klant),
            KlantBtwNummer = klant?.BtwNummer,
            FactuurDatum = now,
            VervalDatum = now.AddDays(30),
            IsBtwVrijgesteld = vrijgesteld,
            Status = FactuurStatus.Draft,
            Lijnen = BuildLijnen(offerte, btwPct, vrijgesteld)
        };

        HerberekenTotalen(factuur);

        db.Facturen.Add(factuur);
        await db.SaveChangesAsync();

        return factuur;
    }

    public async Task<Factuur?> GetFactuurAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
    }

    public async Task MarkeerKlaarVoorExportAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.FindAsync(factuurId) ?? throw new InvalidOperationException("Factuur niet gevonden.");
        if (factuur.Status != FactuurStatus.Draft)
            throw new InvalidOperationException("Alleen draft facturen kunnen klaar gezet worden voor export.");
        factuur.Status = FactuurStatus.KlaarVoorExport;
        factuur.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkeerBetaaldAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.FindAsync(factuurId) ?? throw new InvalidOperationException("Factuur niet gevonden.");
        if (factuur.Status is FactuurStatus.Geannuleerd)
            throw new InvalidOperationException("Geannuleerde factuur kan niet betaald worden.");
        factuur.Status = FactuurStatus.Betaald;
        factuur.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SaveDraftAsync(Factuur updated)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.Include(x => x.Lijnen).FirstOrDefaultAsync(x => x.Id == updated.Id)
            ?? throw new InvalidOperationException("Factuur niet gevonden.");

        if (factuur.Status != FactuurStatus.Draft)
            throw new InvalidOperationException("Alleen draft facturen zijn bewerkbaar.");

        factuur.FactuurDatum = updated.FactuurDatum;
        factuur.VervalDatum = updated.VervalDatum;
        factuur.Opmerking = updated.Opmerking;

        foreach (var lijn in updated.Lijnen.Where(l => l.Id == 0))
        {
            lijn.FactuurId = factuur.Id;
            lijn.Sortering = factuur.Lijnen.Count == 0 ? 1 : factuur.Lijnen.Max(x => x.Sortering) + 1;
            db.FactuurLijnen.Add(lijn);
        }

        HerberekenTotalen(factuur);
        factuur.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task HerberekenTotalenAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.Include(x => x.Lijnen).FirstOrDefaultAsync(x => x.Id == factuurId)
            ?? throw new InvalidOperationException("Factuur niet gevonden.");
        HerberekenTotalen(factuur);
        await db.SaveChangesAsync();
    }

    private static List<FactuurLijn> BuildLijnen(Offerte offerte, decimal btwPct, bool vrijgesteld)
    {
        var lijnen = new List<FactuurLijn>();
        var effectiefBtw = vrijgesteld ? 0m : btwPct;
        var sort = 1;

        foreach (var r in offerte.Regels.OrderBy(x => x.Id))
        {
            var qty = Math.Max(1, r.AantalStuks);
            var unitEx = qty == 0 ? r.TotaalExcl : Math.Round(r.TotaalExcl / qty, 2);
            var omschrijving = string.Join(" | ", new[]
            {
                r.TypeLijst?.Artikelnummer ?? "Lijstwerk",
                $"{r.BreedteCm.ToString("0.##", CultureInfo.InvariantCulture)}x{r.HoogteCm.ToString("0.##", CultureInfo.InvariantCulture)} cm",
                r.Opmerking
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            lijnen.Add(CreateLijn(omschrijving, qty, "st", unitEx, effectiefBtw, sort++));

            if (r.ExtraPrijs > 0)
                lijnen.Add(CreateLijn("Extra kost", 1, "st", r.ExtraPrijs, effectiefBtw, sort++));

            if (r.ExtraWerkMinuten > 0)
                lijnen.Add(CreateLijn($"Extra werk ({r.ExtraWerkMinuten} min)", 1, "st", 0m, effectiefBtw, sort++));
        }

        if (offerte.MeerPrijsIncl > 0)
        {
            var ex = effectiefBtw <= 0 ? offerte.MeerPrijsIncl : offerte.MeerPrijsIncl / (1m + (effectiefBtw / 100m));
            lijnen.Add(CreateLijn("Meerprijs", 1, "st", Math.Round(ex, 2), effectiefBtw, sort));
        }

        return lijnen;
    }

    private static FactuurLijn CreateLijn(string omschrijving, decimal aantal, string eenheid, decimal prijsExcl, decimal btwPct, int sortering)
    {
        var excl = Math.Round(aantal * prijsExcl, 2);
        var btw = Math.Round(excl * (btwPct / 100m), 2);
        return new FactuurLijn
        {
            Omschrijving = omschrijving,
            Aantal = aantal,
            Eenheid = eenheid,
            PrijsExcl = prijsExcl,
            BtwPct = btwPct,
            TotaalExcl = excl,
            TotaalBtw = btw,
            TotaalIncl = excl + btw,
            Sortering = sortering
        };
    }

    private static void HerberekenTotalen(Factuur factuur)
    {
        foreach (var lijn in factuur.Lijnen)
        {
            lijn.TotaalExcl = Math.Round(lijn.Aantal * lijn.PrijsExcl, 2);
            lijn.TotaalBtw = Math.Round(lijn.TotaalExcl * (lijn.BtwPct / 100m), 2);
            lijn.TotaalIncl = lijn.TotaalExcl + lijn.TotaalBtw;
        }

        factuur.TotaalExclBtw = Math.Round(factuur.Lijnen.Sum(l => l.TotaalExcl), 2);
        factuur.TotaalBtw = Math.Round(factuur.Lijnen.Sum(l => l.TotaalBtw), 2);
        factuur.TotaalInclBtw = Math.Round(factuur.TotaalExclBtw + factuur.TotaalBtw, 2);
    }

    private static string BuildKlantNaam(Klant? klant)
        => klant is null ? "Onbekende klant" : $"{klant.Voornaam} {klant.Achternaam}".Trim();

    private static string? BuildAdres(Klant? klant)
    {
        if (klant is null) return null;
        var line1 = string.Join(" ", new[] { klant.Straat, klant.Nummer }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var line2 = string.Join(" ", new[] { klant.Postcode, klant.Gemeente }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.Join(", ", new[] { line1, line2 }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static async Task<decimal> LeesBtwPctAsync(AppDbContext db)
    {
        var waarde = await db.Instellingen.Where(x => x.Sleutel == "BtwPercent").Select(x => x.Waarde).FirstOrDefaultAsync();
        return decimal.TryParse(waarde, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 21m;
    }

    private static async Task<bool> IsBtwVrijgesteldAsync(AppDbContext db)
    {
        var waarde = await db.Instellingen.Where(x => x.Sleutel == "BtwVrijgesteld").Select(x => x.Waarde).FirstOrDefaultAsync();
        return string.Equals(waarde, "true", StringComparison.OrdinalIgnoreCase);
    }
}
