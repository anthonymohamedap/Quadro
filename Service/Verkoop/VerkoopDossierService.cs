using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Verkoop
{
    /// <summary>US-87 — wat er met een voorschot gebeurt bij annuleren (beslist 02/11).</summary>
    public enum VoorschotAfhandeling
    {
        /// <summary>Blijft geboekt (bv. als annuleringskost).</summary>
        Houden,
        /// <summary>Er komt een terugbetaling (negatieve correctie) bij in Overzicht betalingen.</summary>
        Terugbetalen
    }

    /// <summary>US-87 — opdracht om een verkoop te annuleren.</summary>
    public sealed record AnnuleerOpdracht(
        string Reden,
        VoorschotAfhandeling Voorschot,
        Betaalwijze Betaalwijze = Betaalwijze.Bancontact,
        DateTime? Datum = null);

    public interface IVerkoopDossierService
    {
        /// <summary>Wat er nog ontbreekt om met de productie te starten (leeg = alles in orde).</summary>
        Task<IReadOnlyList<string>> WatOntbreektVoorProductieAsync(int offerteId);

        /// <summary>Stap 2 → 3: werkbon In uitvoering, offerte In productie. Alles gepland en afhaaldatum verplicht.</summary>
        Task StartProductieAsync(int offerteId);

        /// <summary>Weeklijst afgedrukt: elke volledig geplande verkoop met werk in die week gaat naar In productie.</summary>
        Task<int> ZetWeekInProductieAsync(DateTime dagInWeek);

        /// <summary>Stap 3 → 4: werkbon Afgewerkt (voorraad verbruikt, bestelbon). Geeft het bestelbonnummer terug.</summary>
        Task<string?> MeldKlaarAsync(int offerteId);

        /// <summary>Stap 2 of 3 overslaan → meteen Klaar om af te halen, met reden.</summary>
        Task<string?> SlaOverNaarKlaarAsync(int offerteId, string reden);

        /// <summary>Wat er al ontvangen werd op deze verkoop (voorschot + betalingen − terugbetalingen).</summary>
        Task<decimal> OntvangenBedragAsync(int offerteId);

        /// <summary>Annuleren met reden en afhandeling van het ontvangen bedrag.</summary>
        Task AnnuleerAsync(int offerteId, AnnuleerOpdracht opdracht);
    }

    /// <summary>
    /// US-87 — één plek voor de stapovergangen van het verkoopdossier. Bouwt op de bestaande
    /// services (<see cref="IWorkflowService"/>): dezelfde statussen, voorraad en bestelbon als vandaag.
    /// De prijsberekening wordt hier niet aangeraakt.
    /// </summary>
    public sealed class VerkoopDossierService : IVerkoopDossierService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IWorkflowService _workflow;
        private readonly IAuthService? _auth;

        public VerkoopDossierService(IDbContextFactory<AppDbContext> factory, IWorkflowService workflow, IAuthService? auth = null)
        {
            _factory = factory;
            _workflow = workflow;
            _auth = auth;
        }

        public async Task<IReadOnlyList<string>> WatOntbreektVoorProductieAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var offerte = await db.Offertes.AsNoTracking()
                .Include(o => o.Regels)
                .Include(o => o.WerkBon)
                .FirstOrDefaultAsync(o => o.Id == offerteId)
                ?? throw new InvalidOperationException("Verkoop niet gevonden.");

            if (offerte.WerkBon is null)
                return new[] { "Bevestig eerst de offerte (Bevestigen → Werkbon)." };

            var taken = await db.WerkTaken.AsNoTracking()
                .Where(t => t.WerkBonId == offerte.WerkBon.Id)
                .Select(t => t.OfferteRegelId)
                .ToListAsync();

            return BepaalWatOntbreekt(offerte, taken);
        }

        /// <summary>
        /// Pure regel (testbaar): alle inlijstingen gepland (beslist 02/11: niet zonder volledige
        /// planning) en een afhaaldatum. Taken zonder regel (oude planning per werkbon) tellen als
        /// "alles gepland".
        /// </summary>
        public static IReadOnlyList<string> BepaalWatOntbreekt(Offerte offerte, IReadOnlyCollection<int?> geplandeRegelIds)
        {
            var ontbreekt = new List<string>();

            var oudePlanning = geplandeRegelIds.Any(id => id is null);
            var nietGepland = oudePlanning
                ? 0
                : offerte.Regels.Count(r => !geplandeRegelIds.Contains(r.Id));
            if (offerte.Regels.Count > 0 && geplandeRegelIds.Count == 0)
                nietGepland = offerte.Regels.Count;
            if (nietGepland > 0)
                ontbreekt.Add(nietGepland == 1
                    ? "Plan eerst alle inlijstingen (1 nog te plannen)."
                    : $"Plan eerst alle inlijstingen ({nietGepland} nog te plannen).");

            var afhaal = offerte.AfhaalDatum ?? offerte.WerkBon?.AfhaalDatum;
            if (afhaal is null)
                ontbreekt.Add("Vul een afhaaldatum in.");

            return ontbreekt;
        }

        public async Task StartProductieAsync(int offerteId)
        {
            var werkBon = await WerkBonVanAsync(offerteId);
            if (werkBon.Status != WerkBonStatus.Gepland)
                throw new InvalidOperationException("De productie is al gestart.");

            var ontbreekt = await WatOntbreektVoorProductieAsync(offerteId);
            if (ontbreekt.Count > 0)
                throw new InvalidOperationException(string.Join(" ", ontbreekt));

            // Zelfde overgang als vandaag via de werkbonstatus: offerte volgt naar In productie.
            await _workflow.ChangeWerkBonStatusAsync(werkBon.Id, WerkBonStatus.InUitvoering);
        }

        public async Task<int> ZetWeekInProductieAsync(DateTime dagInWeek)
        {
            var maandag = ISOWeek.ToDateTime(ISOWeek.GetYear(dagInWeek), ISOWeek.GetWeekOfYear(dagInWeek), DayOfWeek.Monday).Date;
            var volgendeMaandag = maandag.AddDays(7);

            List<int> offerteIds;
            await using (var db = await _factory.CreateDbContextAsync())
            {
                offerteIds = await db.WerkTaken.AsNoTracking()
                    .Where(t => t.GeplandVan >= maandag && t.GeplandVan < volgendeMaandag
                             && t.WerkBon != null && t.WerkBon.Status == WerkBonStatus.Gepland)
                    .Select(t => t.WerkBon!.OfferteId)
                    .Distinct()
                    .ToListAsync();
            }

            var aantal = 0;
            foreach (var id in offerteIds)
            {
                if ((await WatOntbreektVoorProductieAsync(id)).Count > 0)
                    continue;   // niet volledig gepland → blijft in stap 2 (beslist 02/11)
                await StartProductieAsync(id);
                aantal++;
            }
            return aantal;
        }

        public async Task<string?> MeldKlaarAsync(int offerteId)
        {
            var werkBon = await WerkBonVanAsync(offerteId);
            if (werkBon.Status != WerkBonStatus.InUitvoering)
                throw new InvalidOperationException("Start eerst de productie.");

            await _workflow.ChangeWerkBonStatusAsync(werkBon.Id, WerkBonStatus.Afgewerkt);
            return await BestelbonNummerAsync(offerteId, werkBon.Id);
        }

        public async Task<string?> SlaOverNaarKlaarAsync(int offerteId, string reden)
        {
            if (string.IsNullOrWhiteSpace(reden))
                throw new InvalidOperationException("Geef een reden op om stappen over te slaan.");

            var werkBon = await WerkBonVanAsync(offerteId);
            var overgeslagen = werkBon.Status switch
            {
                WerkBonStatus.Gepland => VerkoopStapFlags.Werkbon | VerkoopStapFlags.Productie,
                WerkBonStatus.InUitvoering => VerkoopStapFlags.Productie,
                _ => throw new InvalidOperationException("Overslaan kan enkel vanuit stap 2 of 3.")
            };

            // Stap voor stap door de bestaande transities (geen omweg): voorraad en bestelbon zoals vandaag.
            if (werkBon.Status == WerkBonStatus.Gepland)
                await _workflow.ChangeWerkBonStatusAsync(werkBon.Id, WerkBonStatus.InUitvoering);
            await _workflow.ChangeWerkBonStatusAsync(werkBon.Id, WerkBonStatus.Afgewerkt);

            await using (var db = await _factory.CreateDbContextAsync())
            {
                var offerte = await db.Offertes.FirstAsync(o => o.Id == offerteId);
                offerte.OvergeslagenStappen |= overgeslagen;
                var r = reden.Trim();
                offerte.OverslaanReden = r.Length > 200 ? r[..200] : r;
                await db.SaveChangesAsync();
            }

            return await BestelbonNummerAsync(offerteId, werkBon.Id);
        }

        public async Task<decimal> OntvangenBedragAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            // Betalingen op de bestelbon dragen niet altijd het OfferteId → ook via de bon(nen) van deze verkoop.
            var werkBonId = await db.WerkBonnen.Where(w => w.OfferteId == offerteId).Select(w => (int?)w.Id).FirstOrDefaultAsync();
            var bonIds = await db.Bestelbonnen
                .Where(b => b.OfferteId == offerteId || (werkBonId != null && b.WerkBonId == werkBonId))
                .Select(b => b.Id).ToListAsync();

            // SQLite kan geen SUM op decimal → in het geheugen optellen.
            var bedragen = await db.Ontvangsten.AsNoTracking()
                .Where(o => (o.OfferteId == offerteId || (o.BestelbonId != null && bonIds.Contains(o.BestelbonId.Value)))
                         && o.Soort != OntvangstSoort.Winkelverkoop)
                .Select(o => o.BedragIncl)
                .ToListAsync();
            return Math.Round(bedragen.Sum(), 2);
        }

        public async Task AnnuleerAsync(int offerteId, AnnuleerOpdracht opdracht)
        {
            if (string.IsNullOrWhiteSpace(opdracht.Reden))
                throw new InvalidOperationException("Geef een reden op om de verkoop te annuleren.");

            var ontvangen = await OntvangenBedragAsync(offerteId);
            if (opdracht.Voorschot == VoorschotAfhandeling.Terugbetalen && ontvangen > 0m)
                _auth?.VereisPermissie(Permissie.Bestelbonnen);

            // Eerst annuleren (controleert: niet na afhalen/betalen; voorraad, planning, bon, archief zoals nu).
            await _workflow.AnnuleerOfferteAsync(offerteId, opdracht.Reden);

            if (opdracht.Voorschot != VoorschotAfhandeling.Terugbetalen || ontvangen <= 0m)
                return;

            await using var db = await _factory.CreateDbContextAsync();
            var nummer = await db.Offertes.Where(o => o.Id == offerteId).Select(o => o.OfferteNummer).FirstAsync();
            db.Ontvangsten.Add(new Ontvangst
            {
                Soort = OntvangstSoort.Correctie,
                OfferteId = offerteId,
                Datum = (opdracht.Datum ?? DateTime.Today).Date,
                Betaalwijze = opdracht.Betaalwijze,
                BedragIncl = -ontvangen,
                Omschrijving = $"Terugbetaling — verkoop {nummer} geannuleerd",
                AangemaaktDoor = _auth?.CurrentUser?.GebruikersNaam
            });
            await db.SaveChangesAsync();
        }

        private async Task<WerkBon> WerkBonVanAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.WerkBonnen.AsNoTracking().FirstOrDefaultAsync(w => w.OfferteId == offerteId)
                ?? throw new InvalidOperationException("Bevestig eerst de offerte (Bevestigen → Werkbon).");
        }

        private async Task<string?> BestelbonNummerAsync(int offerteId, int werkBonId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Bestelbonnen.AsNoTracking()
                .Where(b => b.OfferteId == offerteId || b.WerkBonId == werkBonId)
                .Select(b => b.BestelbonNummer)
                .FirstOrDefaultAsync();
        }
    }
}
