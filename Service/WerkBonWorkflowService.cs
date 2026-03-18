using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using System;
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

            // Gebruik het exacte tijdstip als meegegeven; valt terug op 09:00 bij midnight.
            var start = dag.TimeOfDay == TimeSpan.Zero ? dag.Date.AddHours(9) : dag;
            var tot   = start.AddMinutes(duurMinuten);
            var regel = await db.OfferteRegels
                .Include(r => r.TypeLijst)
                .FirstOrDefaultAsync(r => r.Id == offerteRegelId);

            if (regel is null)
                throw new InvalidOperationException("Offerteregel niet gevonden.");

            var benodigdeMeter = CalculateBenodigdeMeter(regel);

            // Bestaande taak voor dezelfde regel updaten (herplannen) i.p.v. duplicate aanmaken.
            var bestaand = await db.WerkTaken.FirstOrDefaultAsync(t =>
                t.WerkBonId      == werkBonId &&
                t.OfferteRegelId == offerteRegelId);

            if (bestaand != null)
            {
                bestaand.GeplandVan  = start;
                bestaand.GeplandTot  = tot;
                bestaand.DuurMinuten = duurMinuten;
                bestaand.BenodigdeMeter = benodigdeMeter;
                if (omschrijving != null) bestaand.Omschrijving = omschrijving;
                await db.SaveChangesAsync();
                return;
            }

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId      = werkBonId,
                OfferteRegelId = offerteRegelId,
                GeplandVan     = start,
                GeplandTot     = tot,
                DuurMinuten    = duurMinuten,
                Omschrijving   = omschrijving ?? "Werktaak",
                BenodigdeMeter = benodigdeMeter
            });

            await db.SaveChangesAsync();

            if (werkBon.Status == WerkBonStatus.Gepland)
                await _workflow.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.InUitvoering);
        }

        public async Task HerplanWerkBonRegelsAsync(int werkBonId, DateTime nieuweDag, int? startUur = 9, int? startMinuut = 0)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.Include(w => w.Taken).FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt || werkBon.Status == WerkBonStatus.Afgehaald)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            var start = nieuweDag.Date.AddHours(startUur ?? 9).AddMinutes(startMinuut ?? 0);

            foreach (var taak in werkBon.Taken.OrderBy(t => t.GeplandVan))
            {
                taak.GeplandVan = start;
                taak.GeplandTot = start.AddMinutes(taak.DuurMinuten);
                start = taak.GeplandTot;
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

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId = werkBonId,
                GeplandVan = start,
                GeplandTot = start.AddMinutes(duurMinuten),
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
    }
}
