using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
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
                Status = WerkBonStatus.Gepland
            };

            db.WerkBonnen.Add(werkBon);
            await db.SaveChangesAsync();

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

            var start = dag.Date.AddHours(9);
            var tot = start.AddMinutes(duurMinuten);

            var exists = await db.WerkTaken.AnyAsync(t =>
                t.WerkBonId == werkBonId &&
                t.OfferteRegelId == offerteRegelId &&
                t.GeplandVan.Date == dag.Date);

            if (exists)
                return;

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId = werkBonId,
                OfferteRegelId = offerteRegelId,
                GeplandVan = start,
                GeplandTot = tot,
                DuurMinuten = duurMinuten,
                Omschrijving = omschrijving ?? "Werktaak"
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
                Omschrijving = omschrijving ?? "Werktaak"
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
    }
}
