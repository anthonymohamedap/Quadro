using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class WerkBonWorkflowService : IWerkBonWorkflowService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IWorkflowService _workflow;

        public WerkBonWorkflowService(IDbContextFactory<AppDbContext> factory, IWorkflowService workflow)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        public async Task<WerkBon> MaakWerkBonAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var bestaand = await db.WerkBonnen.FirstOrDefaultAsync(w => w.OfferteId == offerteId);
            if (bestaand != null)
                return bestaand;

            var offerte = await db.Offertes.FindAsync(offerteId);
            if (offerte == null)
                throw new InvalidOperationException("Offerte niet gevonden.");

            var werkBon = new WerkBon
            {
                OfferteId = offerteId,
                TotaalPrijsIncl = offerte.TotaalInclBtw,
                Status = WerkBonStatus.Gepland,
                StockReservationProcessed = false
            };

            db.WerkBonnen.Add(werkBon);
            await db.SaveChangesAsync();
            await _workflow.ReserveStockForWerkBonAsync(werkBon.Id);

            return werkBon;
        }

        public async Task VoegPlanningToeVoorRegelAsync(int werkBonId, int offerteRegelId, DateTime dag, int duurMinuten, string? omschrijving = null)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt || werkBon.Status == WerkBonStatus.Afgehaald)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            // Blokkeer-check
            var isGeblokkeerd = await db.GeblokkeerDagen.AnyAsync(g => g.Datum == dag.Date);
            if (isGeblokkeerd)
                throw new InvalidOperationException("Deze dag is geblokkeerd.");

            // Alleen de datum telt — tijdstip is 09:00 (dummy)
            var start = dag.Date.AddHours(9);
            var tot = start.AddMinutes(duurMinuten);
            var regel = await db.OfferteRegels
                .Include(r => r.TypeLijst)
                .FirstOrDefaultAsync(r => r.Id == offerteRegelId);

            if (regel is null)
                throw new InvalidOperationException("Offerteregel niet gevonden.");

            var benodigdeMeter = CalculateBenodigdeMeter(regel);

            // Bestaande taak voor dezelfde regel updaten (herplannen) i.p.v. duplicate aanmaken.
            var bestaand = await db.WerkTaken.FirstOrDefaultAsync(t =>
                t.WerkBonId == werkBonId &&
                t.OfferteRegelId == offerteRegelId);

            if (bestaand != null)
            {
                bestaand.GeplandVan = start;
                bestaand.GeplandTot = tot;
                bestaand.DuurMinuten = duurMinuten;
                bestaand.BenodigdeMeter = benodigdeMeter;
                if (omschrijving != null) bestaand.Omschrijving = omschrijving;
                await db.SaveChangesAsync();
                return;
            }

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId = werkBonId,
                OfferteRegelId = offerteRegelId,
                GeplandVan = start,
                GeplandTot = tot,
                DuurMinuten = duurMinuten,
                Omschrijving = omschrijving ?? "Werktaak",
                BenodigdeMeter = benodigdeMeter
            });

            await db.SaveChangesAsync();

            if (werkBon.Status == WerkBonStatus.Gepland)
                await _workflow.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.InUitvoering);
        }

        public async Task<DateTime> PlanRegelMetDagCapaciteitAsync(
            int werkBonId,
            int offerteRegelId,
            DateTime startDag,
            int duurMinuten,
            int capaciteitMinutenPerDag,
            string? omschrijving = null)
        {
            if (duurMinuten <= 0)
                throw new InvalidOperationException("Duur moet groter zijn dan 0 minuten.");

            if (capaciteitMinutenPerDag <= 0)
                throw new InvalidOperationException("Dagcapaciteit moet groter zijn dan 0 minuten.");

            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt || werkBon.Status == WerkBonStatus.Afgehaald)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            var regel = await db.OfferteRegels
                .Include(r => r.TypeLijst)
                .FirstOrDefaultAsync(r => r.Id == offerteRegelId);

            if (regel is null)
                throw new InvalidOperationException("Offerteregel niet gevonden.");

            var geblokkeerdeDagen = await db.GeblokkeerDagen
                .Select(g => g.Datum.Date)
                .ToHashSetAsync();

            var bestaandeTaken = await db.WerkTaken
                .Where(t => t.WerkBonId == werkBonId && t.OfferteRegelId == offerteRegelId)
                .OrderBy(t => t.GeplandVan)
                .ToListAsync();

            var segmenten = new List<(DateTime Dag, int DuurMinuten)>();
            var huidigeDag = startDag.Date;
            var resterendeMinuten = duurMinuten;

            while (resterendeMinuten > 0)
            {
                huidigeDag = await ZoekBeschikbareDagMetVrijeCapaciteitAsync(
                    db,
                    huidigeDag,
                    werkBonId,
                    offerteRegelId,
                    capaciteitMinutenPerDag,
                    geblokkeerdeDagen);

                var bezetOpDag = await GetBezettingVoorDagAsync(db, huidigeDag, werkBonId, offerteRegelId);
                var vrijeMinuten = capaciteitMinutenPerDag - bezetOpDag;
                if (vrijeMinuten <= 0)
                {
                    huidigeDag = huidigeDag.AddDays(1);
                    continue;
                }

                var segmentDuur = Math.Min(resterendeMinuten, vrijeMinuten);
                segmenten.Add((huidigeDag, segmentDuur));
                resterendeMinuten -= segmentDuur;

                if (resterendeMinuten > 0)
                    huidigeDag = huidigeDag.AddDays(1);
            }

            var benodigdeMeters = VerdeelBenodigdeMeters(CalculateBenodigdeMeter(regel), segmenten.Select(x => x.DuurMinuten).ToList());

            for (int i = 0; i < segmenten.Count; i++)
            {
                var segment = segmenten[i];
                var start = segment.Dag.Date.AddHours(9);
                var taakOmschrijving = segmenten.Count == 1
                    ? omschrijving ?? "Werktaak"
                    : $"{omschrijving ?? "Werktaak"} (deel {i + 1}/{segmenten.Count})";

                if (i < bestaandeTaken.Count)
                {
                    var taak = bestaandeTaken[i];
                    taak.GeplandVan = start;
                    taak.GeplandTot = start.AddMinutes(segment.DuurMinuten);
                    taak.DuurMinuten = segment.DuurMinuten;
                    taak.BenodigdeMeter = benodigdeMeters[i];
                    taak.Omschrijving = taakOmschrijving;
                }
                else
                {
                    db.WerkTaken.Add(new WerkTaak
                    {
                        WerkBonId = werkBonId,
                        OfferteRegelId = offerteRegelId,
                        GeplandVan = start,
                        GeplandTot = start.AddMinutes(segment.DuurMinuten),
                        DuurMinuten = segment.DuurMinuten,
                        Omschrijving = taakOmschrijving,
                        BenodigdeMeter = benodigdeMeters[i]
                    });
                }
            }

            foreach (var overtolligeTaak in bestaandeTaken.Skip(segmenten.Count))
                db.WerkTaken.Remove(overtolligeTaak);

            await db.SaveChangesAsync();

            if (werkBon.Status == WerkBonStatus.Gepland)
                await _workflow.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.InUitvoering);

            return segmenten[^1].Dag;
        }

        public async Task HerplanWerkBonRegelsAsync(int werkBonId, DateTime nieuweDag, int? startUur = 9, int? startMinuut = 0)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.Include(w => w.Taken).FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt || werkBon.Status == WerkBonStatus.Afgehaald)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            // Blokkeer-check
            var isGeblokkeerd = await db.GeblokkeerDagen.AnyAsync(g => g.Datum == nieuweDag.Date);
            if (isGeblokkeerd)
                throw new InvalidOperationException("Deze dag is geblokkeerd.");

            var dagStart = nieuweDag.Date.AddHours(9);

            foreach (var taak in werkBon.Taken.OrderBy(t => t.GeplandVan))
            {
                taak.GeplandVan = dagStart;
                taak.GeplandTot = dagStart.AddMinutes(taak.DuurMinuten);
            }

            werkBon.BijgewerktOp = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        public async Task VoegPlanningToeAsync(int werkBonId, DateTime start, int duurMinuten, string? omschrijving = null)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.Include(w => w.Taken).FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt || werkBon.Status == WerkBonStatus.Afgehaald)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            // Blokkeer-check
            var isGeblokkeerd = await db.GeblokkeerDagen.AnyAsync(g => g.Datum == start.Date);
            if (isGeblokkeerd)
                throw new InvalidOperationException("Deze dag is geblokkeerd.");

            var dagStart = start.Date.AddHours(9);

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId = werkBonId,
                GeplandVan = dagStart,
                GeplandTot = dagStart.AddMinutes(duurMinuten),
                DuurMinuten = duurMinuten,
                Omschrijving = omschrijving ?? "Werktaak",
                BenodigdeMeter = 0.01m
            });

            await db.SaveChangesAsync();

            if (werkBon.Status == WerkBonStatus.Gepland)
                await _workflow.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.InUitvoering);
        }

        public Task VeranderStatusAsync(int werkBonId, WerkBonStatus nieuweStatus) =>
            _workflow.ChangeWerkBonStatusAsync(werkBonId, nieuweStatus);

        public Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus nieuweStatus) =>
            _workflow.ChangeWerkBonStatusAsync(werkBonId, nieuweStatus);

        public async Task MarkeerTaakAlsVoltooidAsync(int taakId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var taak = await db.WerkTaken
                .Include(t => t.WerkBon)
                .ThenInclude(w => w.Taken)
                .FirstOrDefaultAsync(t => t.Id == taakId);

            if (taak == null)
                throw new InvalidOperationException("Taak niet gevonden.");

            taak.Resource = "Voltooid";
            await db.SaveChangesAsync();

            var werkBon = taak.WerkBon;
            var allesKlaar = werkBon.Taken.All(t => t.Resource == "Voltooid");
            if (allesKlaar && werkBon.Status == WerkBonStatus.InUitvoering)
                await _workflow.ChangeWerkBonStatusAsync(werkBon.Id, WerkBonStatus.Afgewerkt);
        }

        private static decimal CalculateBenodigdeMeter(OfferteRegel regel)
        {
            if (regel.TypeLijst is null)
                return 0.01m;

            var stuks = Math.Max(1, regel.AantalStuks);
            var lengtePerStuk = (((regel.BreedteCm + regel.HoogteCm) * 2m) + (regel.TypeLijst.BreedteCm * 10m)) / 100m;
            return Math.Round(Math.Max(0.01m, lengtePerStuk * stuks), 2, MidpointRounding.AwayFromZero);
        }

        private static async Task<int> GetBezettingVoorDagAsync(AppDbContext db, DateTime dag, int werkBonId, int offerteRegelId)
        {
            return await db.WerkTaken
                .Where(t => t.GeplandVan.Date == dag.Date)
                .Where(t => t.WerkBonId != werkBonId || t.OfferteRegelId != offerteRegelId)
                .SumAsync(t => t.DuurMinuten);
        }

        private static async Task<DateTime> ZoekBeschikbareDagMetVrijeCapaciteitAsync(
            AppDbContext db,
            DateTime vanafDag,
            int werkBonId,
            int offerteRegelId,
            int capaciteitMinutenPerDag,
            HashSet<DateTime> geblokkeerdeDagen)
        {
            var dag = vanafDag.Date;

            for (int i = 0; i < 365; i++)
            {
                if (geblokkeerdeDagen.Contains(dag))
                {
                    dag = dag.AddDays(1);
                    continue;
                }

                var bezetting = await GetBezettingVoorDagAsync(db, dag, werkBonId, offerteRegelId);
                if (bezetting < capaciteitMinutenPerDag)
                    return dag;

                dag = dag.AddDays(1);
            }

            throw new InvalidOperationException("Geen beschikbare dag gevonden binnen het planningsvenster.");
        }

        private static List<decimal> VerdeelBenodigdeMeters(decimal totaleBenodigdeMeter, IReadOnlyList<int> segmentDuren)
        {
            var verdeling = new List<decimal>(segmentDuren.Count);
            if (segmentDuren.Count == 0)
                return verdeling;

            var totaleDuur = Math.Max(1, segmentDuren.Sum());
            var resterendeMeter = Math.Max(0.01m, totaleBenodigdeMeter);

            for (int i = 0; i < segmentDuren.Count; i++)
            {
                if (i == segmentDuren.Count - 1)
                {
                    verdeling.Add(Math.Round(Math.Max(0.01m, resterendeMeter), 2, MidpointRounding.AwayFromZero));
                    break;
                }

                var minimumVoorResterendeSegmenten = 0.01m * (segmentDuren.Count - i - 1);
                var proportioneel = Math.Round(totaleBenodigdeMeter * segmentDuren[i] / totaleDuur, 2, MidpointRounding.AwayFromZero);
                var segmentMeter = Math.Max(0.01m, proportioneel);
                segmentMeter = Math.Min(segmentMeter, Math.Max(0.01m, resterendeMeter - minimumVoorResterendeSegmenten));

                verdeling.Add(segmentMeter);
                resterendeMeter -= segmentMeter;
            }

            return verdeling;
        }
    }
}
