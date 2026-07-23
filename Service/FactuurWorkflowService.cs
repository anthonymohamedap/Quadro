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
    private readonly IPricingService _pricing;
    private readonly IAuthService _auth;

    public FactuurWorkflowService(IDbContextFactory<AppDbContext> factory, IPricingService pricing, IAuthService auth)
    {
        _factory = factory;
        _pricing = pricing;
        _auth = auth;
    }

    public async Task<Factuur> MaakFactuurVanOfferteAsync(int offerteId)
    {
        _auth.VereisPermissie(Security.Permissie.Factureren);
        await using var db = await _factory.CreateDbContextAsync();

        var offerte = await LoadOfferteAsync(db, offerteId);
        return await GetOrCreateFactuurAsync(db, offerte, werkBonId: null);
    }

    public async Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId)
    {
        _auth.VereisPermissie(Security.Permissie.Factureren);
        await using var db = await _factory.CreateDbContextAsync();

        var werkbon = await db.WerkBonnen
            .Include(w => w.Offerte).ThenInclude(o => o!.Klant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.TypeLijst)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.Glas)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.PassePartout1)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.PassePartout2)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.DiepteKern)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.Opkleven)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.Rug)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.GlasVariant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.PassePartout1Variant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.PassePartout2Variant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.DiepteKernVariant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.OpklevenVariant)
            .Include(w => w.Offerte).ThenInclude(o => o!.Regels).ThenInclude(r => r.RugVariant)
            .FirstOrDefaultAsync(w => w.Id == werkBonId);

        if (werkbon is null)
            throw new InvalidOperationException("Werkbon niet gevonden.");

        var offerte = werkbon.Offerte ?? throw new InvalidOperationException("Werkbon heeft geen offerte.");
        return await GetOrCreateFactuurAsync(db, offerte, werkBonId);
    }

    public async Task<Factuur?> GetFactuurAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
    }

    public async Task<Factuur?> GetFactuurVoorOfferteAsync(int offerteId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var factuur = await db.Facturen
            .Include(x => x.Lijnen.OrderBy(l => l.Sortering))
            .FirstOrDefaultAsync(x => x.OfferteId == offerteId || (x.WerkBon != null && x.WerkBon.OfferteId == offerteId));

        if (factuur is not null && factuur.OfferteId != offerteId)
        {
            factuur.OfferteId = offerteId;
            await db.SaveChangesAsync();
        }

        return factuur;
    }

    public async Task MarkeerKlaarVoorExportAsync(int factuurId)
    {
        _auth.VereisPermissie(Security.Permissie.Factureren);
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
        _auth.VereisPermissie(Security.Permissie.Factureren);
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
        _auth.VereisPermissie(Security.Permissie.Factureren);
        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.Include(x => x.Lijnen).FirstOrDefaultAsync(x => x.Id == updated.Id)
            ?? throw new InvalidOperationException("Factuur niet gevonden.");

        if (factuur.Status != FactuurStatus.Draft)
            throw new InvalidOperationException("Alleen draft facturen zijn bewerkbaar.");

        factuur.OfferteId = updated.OfferteId ?? factuur.OfferteId;
        factuur.WerkBonId = updated.WerkBonId ?? factuur.WerkBonId;
        factuur.FactuurDatum = updated.FactuurDatum;
        factuur.VervalDatum = updated.VervalDatum;
        factuur.GeplandeDatum = updated.GeplandeDatum;
        factuur.AfhaalDatum = updated.AfhaalDatum;
        factuur.Opmerking = updated.Opmerking;
        factuur.AangenomenDoorInitialen = updated.AangenomenDoorInitialen;
        factuur.VoorschotBedrag = updated.VoorschotBedrag;

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

            // Afgesproken prijs is autoritair op de bestelbon: gebruik hem RECHTSTREEKS
            // i.p.v. het (mogelijk verouderde) berekende regeltotaal. De afgesproken prijs
            // wordt door de gebruiker INCL btw ingegeven → terugrekenen naar excl per stuk
            // (zelfde semantiek als PricingEngine).
            decimal unitEx;
            if (r.AfgesprokenPrijsExcl.HasValue)
            {
                // NIET afronden: volledige precisie zodat CreateLijn de incl.-prijs
                // exact reproduceert (bv. € 100 blijft € 100, niet € 99,99).
                unitEx = effectiefBtw <= 0
                    ? r.AfgesprokenPrijsExcl.Value
                    : r.AfgesprokenPrijsExcl.Value / (1m + (effectiefBtw / 100m));
            }
            else
            {
                unitEx = qty == 0 ? r.TotaalExcl : Math.Round(r.TotaalExcl / qty, 2);
            }

            // Bouw verrijkte pipe-string met tagged segmenten
            var segments = new List<string>
            {
                r.TypeLijst?.Artikelnummer ?? "Lijstwerk",
                $"{r.BreedteCm.ToString("0.##", CultureInfo.InvariantCulture)}x{r.HoogteCm.ToString("0.##", CultureInfo.InvariantCulture)} cm"
            };

            // Titel (tagged)
            if (!string.IsNullOrWhiteSpace(r.Titel))
                segments.Add($"titel:{r.Titel}");

            // Afwerkingen (tagged) — Naam + kleurvariant indien relevant
            if (r.Glas is not null)
                segments.Add($"glas:{AfwLabel(r.Glas, r.GlasVariant)}");
            if (r.PassePartout1 is not null)
                segments.Add($"pp1:{AfwLabel(r.PassePartout1, r.PassePartout1Variant)}");
            if (r.PassePartout2 is not null)
                segments.Add($"pp2:{AfwLabel(r.PassePartout2, r.PassePartout2Variant)}");
            if (r.DiepteKern is not null)
                segments.Add($"diepte:{AfwLabel(r.DiepteKern, r.DiepteKernVariant)}");
            if (r.Opkleven is not null)
                segments.Add($"opkleven:{AfwLabel(r.Opkleven, r.OpklevenVariant)}");
            if (r.Rug is not null)
                segments.Add($"rug:{AfwLabel(r.Rug, r.RugVariant)}");

            // TypeLijst opmerking (tagged)
            if (!string.IsNullOrWhiteSpace(r.TypeLijst?.Opmerking))
                segments.Add($"lijst_opm:{r.TypeLijst!.Opmerking}");

            // OfferteRegel opmerking (tagged)
            if (!string.IsNullOrWhiteSpace(r.Opmerking))
                segments.Add($"opm:{r.Opmerking}");

            // Afhaal datum per regel (tagged)
            if (r.AfhaalDatum.HasValue)
                segments.Add($"afhaal:{r.AfhaalDatum.Value:yyyy-MM-dd}");

            var omschrijving = string.Join(" | ", segments.Where(x => !string.IsNullOrWhiteSpace(x)));

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

    /// <summary>
    /// Formatteert een afwerkingsoptie + gekozen variant als leesbaar label.
    /// Met variant: "Mat Glas — Brons". Zonder variant: "Mat Glas (Kleur)" of "Mat Glas".
    /// </summary>
    private static string AfwLabel(AfwerkingsOptie o, AfwerkingsVariant? variant = null)
    {
        var bs = variant?.Beschrijving?.Trim();
        if (!string.IsNullOrEmpty(bs) && !bs.Equals("Standaard", StringComparison.OrdinalIgnoreCase))
            return $"{o.Naam} — {bs}";

        var kleur = o.Kleur?.Trim();
        return string.IsNullOrEmpty(kleur) || kleur.Equals("Standaard", StringComparison.OrdinalIgnoreCase)
            ? o.Naam
            : $"{o.Naam} ({kleur})";
    }

    private static FactuurLijn CreateLijn(string omschrijving, decimal aantal, string eenheid, decimal prijsExcl, decimal btwPct, int sortering)
    {
        // BTW = bruto − netto: bereken de incl. prijs uit de (volledige-precisie) netto
        // prijs en leid de BTW af als het verschil. Hierdoor blijft een afgesproken
        // incl.-prijs (bv. € 100) exact behouden i.p.v. te verliezen op afronding (€ 99,99).
        var excl = Math.Round(aantal * prijsExcl, 2);
        var incl = Math.Round(aantal * prijsExcl * (1m + (btwPct / 100m)), 2);
        return new FactuurLijn
        {
            Omschrijving = omschrijving,
            Aantal = aantal,
            Eenheid = eenheid,
            PrijsExcl = prijsExcl,
            BtwPct = btwPct,
            TotaalExcl = excl,
            TotaalBtw = incl - excl,
            TotaalIncl = incl,
            Sortering = sortering
        };
    }

    private static void HerberekenTotalen(Factuur factuur)
    {
        foreach (var lijn in factuur.Lijnen)
        {
            // BTW = bruto − netto (incl. uit volledige-precisie netto), zodat een
            // afgesproken incl.-prijs exact behouden blijft (zie CreateLijn).
            lijn.TotaalExcl = Math.Round(lijn.Aantal * lijn.PrijsExcl, 2);
            lijn.TotaalIncl = Math.Round(lijn.Aantal * lijn.PrijsExcl * (1m + (lijn.BtwPct / 100m)), 2);
            lijn.TotaalBtw = lijn.TotaalIncl - lijn.TotaalExcl;
        }

        var brutoExcl = Math.Round(factuur.Lijnen.Sum(l => l.TotaalExcl), 2);
        var brutoBtw = Math.Round(factuur.Lijnen.Sum(l => l.TotaalBtw), 2);

        // US-28: korting wordt berekend op het incl.-BTW-bedrag (brutoIncl).
        // KortingBedragExcl slaat het incl.-kortingbedrag op voor weergave op de PDF.
        // De effectieve BTW-factor wordt afgeleid van de lijnen zodat geen aparte
        // BtwPercent-kolom nodig is op Factuur — werkt ook correct bij gemengde tarieven
        // en BTW-vrijstelling (btwFactor = 0 → TotaalExclBtw = TotaalInclBtw).
        if (factuur.KortingPct > 0m && brutoExcl > 0m)
        {
            var brutoIncl = Math.Round(brutoExcl + brutoBtw, 2);
            var kortingIncl = Math.Round(brutoIncl * (factuur.KortingPct / 100m), 2);
            if (kortingIncl > brutoIncl) kortingIncl = brutoIncl;

            // Gewogen gemiddelde BTW-factor over alle lijnen.
            var btwFactor = brutoExcl > 0m ? brutoBtw / brutoExcl : 0m;

            factuur.KortingBedragExcl = kortingIncl;            // incl.-bedrag bewaard voor PDF (US-28)
            factuur.TotaalInclBtw     = Math.Round(brutoIncl - kortingIncl, 2);
            factuur.TotaalExclBtw     = btwFactor > 0m
                ? Math.Round(factuur.TotaalInclBtw / (1m + btwFactor), 2)
                : factuur.TotaalInclBtw;
            factuur.TotaalBtw         = factuur.TotaalInclBtw - factuur.TotaalExclBtw;
        }
        else
        {
            factuur.KortingBedragExcl = 0m;
            factuur.TotaalExclBtw = brutoExcl;
            factuur.TotaalBtw = brutoBtw;
            factuur.TotaalInclBtw = Math.Round(brutoExcl + brutoBtw, 2);
        }
    }

    private static string BuildKlantNaam(Klant? klant)
        => klant is null ? "Onbekende klant" : $"{klant.Voornaam} {klant.Achternaam}".Trim();

    private static string? BuildAdres(Klant? klant)
    {
        if (klant is null) return null;
        var line1 = string.Join(" ", new[] { klant.Straat, klant.Nummer }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var line2 = string.Join(" ", new[] { klant.Postcode, klant.Gemeente }.Where(x => !string.IsNullOrWhiteSpace(x)));
        // Postcode + gemeente op een nieuwe regel onder de straat.
        return string.Join("\n", new[] { line1, line2 }.Where(x => !string.IsNullOrWhiteSpace(x)));
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

    private static async Task<Offerte> LoadOfferteAsync(AppDbContext db, int offerteId)
    {
        var offerte = await db.Offertes
            .Include(o => o.Klant)
            .Include(o => o.Regels).ThenInclude(r => r.TypeLijst)
            .Include(o => o.Regels).ThenInclude(r => r.Glas)
            .Include(o => o.Regels).ThenInclude(r => r.PassePartout1)
            .Include(o => o.Regels).ThenInclude(r => r.PassePartout2)
            .Include(o => o.Regels).ThenInclude(r => r.DiepteKern)
            .Include(o => o.Regels).ThenInclude(r => r.Opkleven)
            .Include(o => o.Regels).ThenInclude(r => r.Rug)
            .Include(o => o.Regels).ThenInclude(r => r.GlasVariant)
            .Include(o => o.Regels).ThenInclude(r => r.PassePartout1Variant)
            .Include(o => o.Regels).ThenInclude(r => r.PassePartout2Variant)
            .Include(o => o.Regels).ThenInclude(r => r.DiepteKernVariant)
            .Include(o => o.Regels).ThenInclude(r => r.OpklevenVariant)
            .Include(o => o.Regels).ThenInclude(r => r.RugVariant)
            .FirstOrDefaultAsync(o => o.Id == offerteId);

        return offerte ?? throw new InvalidOperationException("Offerte niet gevonden.");
    }

    private async Task<Factuur> GetOrCreateFactuurAsync(AppDbContext db, Offerte offerte, int? werkBonId)
    {
        var existing = await db.Facturen
            .Include(x => x.Lijnen)
            .FirstOrDefaultAsync(x => x.OfferteId == offerte.Id || (werkBonId.HasValue && x.WerkBonId == werkBonId.Value));

        if (existing is not null)
        {
            var changed = false;

            if (existing.OfferteId != offerte.Id)
            {
                existing.OfferteId = offerte.Id;
                changed = true;
            }

            if (werkBonId.HasValue && existing.WerkBonId != werkBonId)
            {
                existing.WerkBonId = werkBonId;
                changed = true;
            }

            // Sync planning dates and voorschot from offerte so the PDF always reflects
            // the latest values even if the bestelbon was created earlier.
            if (existing.GeplandeDatum != offerte.GeplandeDatum)
            {
                existing.GeplandeDatum = offerte.GeplandeDatum;
                changed = true;
            }
            if (existing.AfhaalDatum != offerte.AfhaalDatum)
            {
                existing.AfhaalDatum = offerte.AfhaalDatum;
                changed = true;
            }
            if (existing.VoorschotBedrag != offerte.VoorschotBedrag)
            {
                existing.VoorschotBedrag = offerte.VoorschotBedrag;
                changed = true;
            }

            if (NeedsDraftPrijsRefresh(existing))
            {
                var btwPctExisting = await LeesBtwPctAsync(db);
                var vrijgesteldExisting = await IsBtwVrijgesteldAsync(db);
                await RebuildDraftLijnenAsync(db, existing, offerte, btwPctExisting, vrijgesteldExisting);
                changed = true;
            }

            if (changed)
                await db.SaveChangesAsync();

            return existing;
        }

        await EnsureOfferteCalculatedAsync(offerte);

        var now = DateTime.Today;
        var jaar = now.Year;

        var btwPct = await LeesBtwPctAsync(db);
        var vrijgesteld = await IsBtwVrijgesteldAsync(db);
        var klant = offerte.Klant;

        var factuur = new Factuur
        {
            OfferteId = offerte.Id,
            WerkBonId = werkBonId,
            Jaar = jaar,
            DocumentType = "Factuur",
            KlantNaam = BuildKlantNaam(klant),
            KlantAdres = BuildAdres(klant),
            KlantBtwNummer = klant?.BtwNummer,
            FactuurDatum = now,
            VervalDatum = now.AddDays(30),
            GeplandeDatum = offerte.GeplandeDatum,
            AfhaalDatum = offerte.AfhaalDatum,
            IsBtwVrijgesteld = vrijgesteld,
            VoorschotBedrag = offerte.VoorschotBedrag,
            KortingPct = offerte.KortingPct,   // US-23
            Status = FactuurStatus.Draft,
            Lijnen = BuildLijnen(offerte, btwPct, vrijgesteld)
        };

        HerberekenTotalen(factuur);

        // US-38: nummer-toekenning met retry. De unieke index op (Jaar, VolgNr) /
        // FactuurNummer laat een gelijktijdige insert falen (DbUpdateException);
        // in dat geval herberekenen we het volgnummer en proberen opnieuw i.p.v.
        // stil een duplicaat nummer weg te schrijven.
        db.Facturen.Add(factuur);

        const int maxPogingen = 5;
        for (var poging = 1; ; poging++)
        {
            factuur.VolgNr = (await db.Facturen.Where(f => f.Jaar == jaar)
                                               .MaxAsync(f => (int?)f.VolgNr) ?? 0) + 1;
            factuur.FactuurNummer = $"{jaar}-{factuur.VolgNr}";

            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (poging < maxPogingen)
            {
                // Nummer is intussen door een andere sessie ingenomen — opnieuw proberen.
            }
        }

        return factuur;
    }

    private async Task EnsureOfferteCalculatedAsync(Offerte offerte)
    {
        if (offerte.Regels.Count == 0)
            return;

        var heeftPrijsData = offerte.Regels.Any(r => r.TotaalExcl > 0m || r.SubtotaalExBtw > 0m || r.TotaalInclBtw > 0m)
            || offerte.SubtotaalExBtw > 0m
            || offerte.TotaalInclBtw > 0m;

        if (heeftPrijsData)
            return;

        await _pricing.BerekenAsync(offerte);
    }

    private static bool NeedsDraftPrijsRefresh(Factuur factuur)
    {
        // Rebuild for Draft: prices or offerte content may have changed.
        if (factuur.Status == FactuurStatus.Draft)
            return true;

        // Also rebuild for KlaarVoorExport when afwerking tags are missing from
        // stored Omschrijving — happens when the factuur was created before
        // afwerkingen were set on the offerte, or before tagged format was added.
        // Safe: we never change the status here, only refresh the line content.
        if (factuur.Status == FactuurStatus.KlaarVoorExport)
        {
            return factuur.Lijnen.Any(l =>
                l.Omschrijving != null &&
                l.Omschrijving.Contains('|') &&          // tagged format is present
                !l.Omschrijving.Contains("glas:")   &&  // but afwerking tags are missing
                !l.Omschrijving.Contains("pp1:")    &&
                !l.Omschrijving.Contains("pp2:")    &&
                !l.Omschrijving.Contains("diepte:") &&
                !l.Omschrijving.Contains("opkleven:") &&
                !l.Omschrijving.Contains("rug:"));
        }

        return false;
    }

    private async Task RebuildDraftLijnenAsync(AppDbContext db, Factuur factuur, Offerte offerte, decimal btwPct, bool vrijgesteld)
    {
        await EnsureOfferteCalculatedAsync(offerte);

        if (factuur.Lijnen.Count > 0)
            db.FactuurLijnen.RemoveRange(factuur.Lijnen);

        factuur.Lijnen.Clear();

        factuur.KortingPct = offerte.KortingPct;   // US-23: korting up-to-date houden voor drafts

        foreach (var lijn in BuildLijnen(offerte, btwPct, vrijgesteld))
            factuur.Lijnen.Add(lijn);

        HerberekenTotalen(factuur);
        factuur.BijgewerktOp = DateTime.UtcNow;
    }
}
