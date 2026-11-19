using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Verkoop
{
    /// <summary>US-84 — tabbladen van de offertelijst, dezelfde fase-indeling als de stappenbalk (US-83).</summary>
    public enum VerkoopFase
    {
        OpenOffertes,
        LopendeWerkbonnen,
        KlaarOmAfTeHalen,
        Afgerond,
        Geannuleerd
    }

    /// <summary>US-84 — één offerte in het overzicht, met haar stap en wat er nog te betalen is.</summary>
    public sealed record VerkoopOverzichtRegel(
        int OfferteId,
        int OfferteNummer,
        string KlantNaam,
        string? KlantEmail,
        DateTime Datum,
        decimal TotaalIncl,
        DateTime? AfhaalDatum,
        VerkoopStapInfo Stap)
    {
        public VerkoopFase Fase => VerkoopOverzichtService.FaseVan(Stap);

        /// <summary>Leesbare fase (in plaats van de enumnaam van de offertestatus, US-85).</summary>
        public string FaseTekst => Stap switch
        {
            { IsGeannuleerd: true } => "Geannuleerd",
            { IsAfgerond: true } => "Afgerond",
            { IsAfgehaaldNogTeBetalen: true } s => $"Afgehaald — nog € {s.NogTeBetalen:0.00}",
            var s => VerkoopStapResolver.Titel(s.Huidig)
        };

        /// <summary>Nummer van de bestelbon (2026-19); leeg zolang er geen (definitieve) bon is.</summary>
        public string? BestelbonNummer { get; init; }

        /// <summary>US-89 — er is een voorlopige bestelbon (nog zonder volgnummer).</summary>
        public bool HeeftVoorlopigeBon { get; init; }

        /// <summary>US-89 — kolom Bestelbon: "2026-24", "voorlopig" of "—".</summary>
        public string BestelbonKolomTekst =>
            !string.IsNullOrWhiteSpace(BestelbonNummer) ? BestelbonNummer!
            : HeeftVoorlopigeBon ? "voorlopig" : "—";

        // ── US-89: wat de Werkbonnenlijst toont, onder de fase ────────────────
        public int? WerkBonId { get; init; }
        public WerkBonStatus? WerkBonStatus { get; init; }
        public int AantalTaken { get; init; }
        public int AantalBesteldOfKlaar { get; init; }

        /// <summary>"Werkbon 426 · In uitvoering · 2 taken · 1/2 besteld" (werkbon = verkoopnummer).</summary>
        public string WerkbonInfo
        {
            get
            {
                if (WerkBonStatus is not { } st || Fase is VerkoopFase.Geannuleerd) return "";
                var delen = new List<string> { $"Werkbon {OfferteNummer}", st.Label() };
                delen.Add(AantalTaken == 1 ? "1 taak" : $"{AantalTaken} taken");
                if (AantalTaken > 0)
                    delen.Add($"{AantalBesteldOfKlaar}/{AantalTaken} besteld");
                return string.Join(" · ", delen);
            }
        }

        public bool ToontWerkbonInfo => WerkbonInfo.Length > 0;

        /// <summary>Openstaand bedrag (0 als alles betaald is).</summary>
        public decimal NogTeBetalen => Stap.NogTeBetalen;

        /// <summary>US-86 — tweede regel op het beginscherm: "Offerte 421 · Bestelbon 2026-23".</summary>
        public string BonTekst => string.IsNullOrWhiteSpace(BestelbonNummer)
            ? $"Offerte {OfferteNummer}"
            : $"Offerte {OfferteNummer} · Bestelbon {BestelbonNummer}";

        /// <summary>US-86 — bedrag rechts op het beginscherm.</summary>
        public string NogTeBetalenTekst => NogTeBetalen > 0m ? $"nog € {NogTeBetalen:0.00}" : "betaald";

        /// <summary>US-86 — korte stap onder het bedrag, zonder het bedrag te herhalen.</summary>
        public string StapKortTekst => Stap switch
        {
            { IsAfgehaaldNogTeBetalen: true } => "Al afgehaald",
            { IsGeannuleerd: false, IsAfgerond: false, Huidig: VerkoopStap.KlaarOmAfTeHalen } => "Klaar",
            _ => FaseTekst
        };

        /// <summary>US-105 — "afgehaald 25/10" in het blok "Nog te betalen".</summary>
        public string AfhaalDatumTekst => AfhaalDatum is { } d ? $"afgehaald {d:dd/MM}" : "";

        /// <summary>US-105 — bedrag in de compacte rij: "€ 136,50".</summary>
        public string BedragTekst => $"€ {NogTeBetalen:#,##0.00}";

        public bool ToontAfhaalDatum => AfhaalDatum.HasValue && Fase is VerkoopFase.LopendeWerkbonnen or VerkoopFase.KlaarOmAfTeHalen;
    }

    public interface IVerkoopOverzichtService
    {
        Task<IReadOnlyList<VerkoopOverzichtRegel>> GetOverzichtAsync();

        /// <summary>US-90 — dezelfde regel voor één verkoop (kop en stappenbalk van het dossier).</summary>
        Task<VerkoopOverzichtRegel?> GetRegelAsync(int offerteId);
    }

    /// <summary>US-86 — de twee blokken van het beginscherm.</summary>
    public sealed record BeginschermBlokken(
        IReadOnlyList<VerkoopOverzichtRegel> VandaagAfTeHalen,
        IReadOnlyList<VerkoopOverzichtRegel> NogTeBetalen)
    {
        /// <summary>
        /// Vandaag af te halen: afhaaldatum vandaag en klaar om af te halen (of al afgehaald, maar
        /// met een rest). Nog te betalen: afgehaald met een rest &gt; 0, oudste afhaaldatum eerst.
        /// </summary>
        public static BeginschermBlokken Bepaal(IEnumerable<VerkoopOverzichtRegel> regels, DateTime vandaag)
        {
            var lijst = regels.ToList();

            var vandaagAf = lijst
                .Where(r => r.AfhaalDatum?.Date == vandaag.Date
                         && (r.Stap is { IsGeannuleerd: false, IsAfgerond: false, Huidig: VerkoopStap.KlaarOmAfTeHalen }
                             || r.Stap.IsAfgehaaldNogTeBetalen))
                .OrderBy(r => r.KlantNaam, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.OfferteNummer)
                .ToList();

            var nogTeBetalen = lijst
                .Where(r => r.Stap.IsAfgehaaldNogTeBetalen)
                .OrderBy(r => r.AfhaalDatum ?? DateTime.MaxValue)
                .ThenBy(r => r.OfferteNummer)
                .ToList();

            return new BeginschermBlokken(vandaagAf, nogTeBetalen);
        }
    }

    /// <summary>
    /// US-84 — leest alle offertes met hun werkbon, bestelbon en betalingen in één keer en bepaalt
    /// per offerte de stap via <see cref="VerkoopStapResolver"/>. Basis voor de offertelijst per fase
    /// en later het beginscherm (US-86) en het scherm Verkopen (US-89).
    /// </summary>
    public sealed class VerkoopOverzichtService : IVerkoopOverzichtService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public VerkoopOverzichtService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public static VerkoopFase FaseVan(VerkoopStapInfo stap) => stap switch
        {
            { IsGeannuleerd: true } => VerkoopFase.Geannuleerd,
            { IsAfgerond: true } => VerkoopFase.Afgerond,
            { Huidig: VerkoopStap.Offerte } => VerkoopFase.OpenOffertes,
            { Huidig: VerkoopStap.Werkbon or VerkoopStap.InProductie } => VerkoopFase.LopendeWerkbonnen,
            // Klaar om af te halen, en afgehaald maar nog niet (volledig) betaald.
            _ => VerkoopFase.KlaarOmAfTeHalen
        };

        public Task<IReadOnlyList<VerkoopOverzichtRegel>> GetOverzichtAsync() => LeesAsync(null);

        public async Task<VerkoopOverzichtRegel?> GetRegelAsync(int offerteId)
            => (await LeesAsync(offerteId)).FirstOrDefault();

        private async Task<IReadOnlyList<VerkoopOverzichtRegel>> LeesAsync(int? offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var offertes = await db.Offertes.AsNoTracking()
                .Where(o => offerteId == null || o.Id == offerteId)
                .Include(o => o.Klant)
                .Include(o => o.WerkBon)
                .OrderByDescending(o => o.Datum).ThenByDescending(o => o.OfferteNummer)
                .ToListAsync();

            var bestelbonnen = await db.Bestelbonnen.AsNoTracking()
                .Select(f => new { f.Id, f.OfferteId, f.WerkBonId, f.Status, f.TotaalInclBtw, f.VoorschotBedrag, f.VolgNr, f.BestelbonNummer })
                .ToListAsync();

            // SQLite kan geen SUM op decimal → in het geheugen optellen (zelfde aanpak als OntvangstService).
            var betalingen = (await db.Ontvangsten.AsNoTracking()
                    .Where(o => o.BestelbonId != null && o.Soort == OntvangstSoort.Betaling)
                    .Select(o => new { BestelbonId = o.BestelbonId!.Value, o.BedragIncl })
                    .ToListAsync())
                .GroupBy(o => o.BestelbonId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.BedragIncl));

            // US-89 — tellers zoals de Werkbonnenlijst (aantal taken, bestelvoortgang).
            var taken = (await db.WerkTaken.AsNoTracking()
                    .Select(t => new { t.WerkBonId, t.IsBesteld, t.IsOpVoorraad })
                    .ToListAsync())
                .GroupBy(t => t.WerkBonId)
                .ToDictionary(g => g.Key, g => (Aantal: g.Count(), Besteld: g.Count(t => t.IsBesteld || t.IsOpVoorraad)));

            var perOfferte = bestelbonnen.Where(f => f.OfferteId != null)
                .GroupBy(f => f.OfferteId!.Value).ToDictionary(g => g.Key, g => g.First());
            var perWerkBon = bestelbonnen.Where(f => f.WerkBonId != null)
                .GroupBy(f => f.WerkBonId!.Value).ToDictionary(g => g.Key, g => g.First());

            var regels = new List<VerkoopOverzichtRegel>(offertes.Count);
            foreach (var o in offertes)
            {
                var bestelbon = perOfferte.GetValueOrDefault(o.Id)
                    ?? (o.WerkBon is null ? null : perWerkBon.GetValueOrDefault(o.WerkBon.Id));

                var rest = bestelbon is null
                    ? o.TotaalInclBtw - o.VoorschotBedrag
                    : bestelbon.TotaalInclBtw - bestelbon.VoorschotBedrag - betalingen.GetValueOrDefault(bestelbon.Id);

                var stap = VerkoopStapResolver.Bepaal(o.Status, o.WerkBon?.Status, bestelbon?.Status, Math.Round(rest, 2), o.OvergeslagenStappen);

                var klantNaam = o.Klant is null ? "" : $"{o.Klant.Voornaam} {o.Klant.Achternaam}".Trim();
                regels.Add(new VerkoopOverzichtRegel(
                    o.Id, o.OfferteNummer, klantNaam, o.Klant?.Email, o.Datum, o.TotaalInclBtw,
                    o.AfhaalDatum ?? o.WerkBon?.AfhaalDatum, stap)
                {
                    // Voorlopige bon (nog geen volgnummer) heeft geen nummer om te tonen.
                    BestelbonNummer = bestelbon?.VolgNr is null ? null : bestelbon.BestelbonNummer,
                    HeeftVoorlopigeBon = bestelbon is not null && bestelbon.VolgNr is null,
                    WerkBonId = o.WerkBon?.Id,
                    WerkBonStatus = o.WerkBon?.Status,
                    AantalTaken = o.WerkBon is null ? 0 : taken.GetValueOrDefault(o.WerkBon.Id).Aantal,
                    AantalBesteldOfKlaar = o.WerkBon is null ? 0 : taken.GetValueOrDefault(o.WerkBon.Id).Besteld
                });
            }

            return regels;
        }
    }
}
