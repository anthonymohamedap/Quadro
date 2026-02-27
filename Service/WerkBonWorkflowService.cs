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


        public WerkBonWorkflowService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<WerkBon> MaakWerkBonAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            // ✅ voorkom dubbel
            var bestaand = await db.WerkBonnen
                .FirstOrDefaultAsync(w => w.OfferteId == offerteId);

            if (bestaand != null)
                return bestaand;

            var offerte = await db.Offertes.FindAsync(offerteId);
            if (offerte == null)
                throw new InvalidOperationException("Offerte niet gevonden.");

            var werkBon = new WerkBon
            {
                OfferteId = offerteId,
                TotaalPrijsIncl = offerte.TotaalInclBtw,
                Status = WerkBonStatus.Nieuw
            };

            db.WerkBonnen.Add(werkBon);
            await db.SaveChangesAsync();

            return werkBon;
        }
        public async Task VoegPlanningToeVoorRegelAsync(
    int werkBonId,
    int offerteRegelId,
    DateTime dag,          // alleen datum, tijd bepalen we hier
    int duurMinuten,
    string? omschrijving = null)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            // start standaard om 09:00 (kan later uitbreiden)
            var start = dag.Date.AddHours(9);
            var tot = start.AddMinutes(duurMinuten);

            // (optioneel) voorkom dubbele planning van dezelfde regel op dezelfde dag:
            var exists = await db.WerkTaken.AnyAsync(t =>
                t.WerkBonId == werkBonId &&
                t.OfferteRegelId == offerteRegelId &&
                t.GeplandVan.Date == dag.Date);

            if (exists)
                return;

            db.WerkTaken.Add(new WerkTaak
            {
                WerkBonId = werkBonId,
                OfferteRegelId = offerteRegelId,   // ✅ je migratie
                GeplandVan = start,
                GeplandTot = tot,
                DuurMinuten = duurMinuten,
                Omschrijving = omschrijving ?? "Werktaak"
            });

            if (werkBon.Status == WerkBonStatus.Nieuw)
                werkBon.Status = WerkBonStatus.InPlanning;

            await db.SaveChangesAsync();
        }
        public async Task HerplanWerkBonRegelsAsync(int werkBonId, DateTime nieuweDag, int? startUur = 9, int? startMinuut = 0)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen
                .Include(w => w.Taken)
                .FirstOrDefaultAsync(w => w.Id == werkBonId);

            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            // nieuwe starttijd (default 09:00)
            var start = nieuweDag.Date.AddHours(startUur ?? 9).AddMinutes(startMinuut ?? 0);

            // als je meerdere taken hebt: zet ze achter elkaar op dezelfde dag
            // (behoud duur, schuif start telkens op)
            foreach (var taak in werkBon.Taken.OrderBy(t => t.GeplandVan))
            {
                taak.GeplandVan = start;
                taak.GeplandTot = start.AddMinutes(taak.DuurMinuten);
                start = taak.GeplandTot;
            }

            if (werkBon.Status == WerkBonStatus.Nieuw)
                werkBon.Status = WerkBonStatus.InPlanning;

            werkBon.BijgewerktOp = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }
        public async Task VoegPlanningToeAsync(
            int werkBonId,
            DateTime start,
            int duurMinuten,
            string? omschrijving = null)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen
                .Include(w => w.Taken)
                .FirstOrDefaultAsync(w => w.Id == werkBonId);

            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (werkBon.Status == WerkBonStatus.Afgewerkt)
                throw new InvalidOperationException("Afgewerkte werkbon kan niet opnieuw gepland worden.");

            var taak = new WerkTaak
            {
                WerkBonId = werkBonId,
                GeplandVan = start,
                GeplandTot = start.AddMinutes(duurMinuten),
                DuurMinuten = duurMinuten,
                Omschrijving = omschrijving ?? "Werktaak"
            };

            db.WerkTaken.Add(taak);

            if (werkBon.Status == WerkBonStatus.Nieuw)
                werkBon.Status = WerkBonStatus.InPlanning;

            await db.SaveChangesAsync();
        }

        public async Task VeranderStatusAsync(int werkBonId, WerkBonStatus nieuweStatus)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FindAsync(werkBonId);
            if (werkBon == null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            if (!IsValidTransition(werkBon.Status, nieuweStatus))
                throw new InvalidOperationException("Ongeldige statusovergang.");

            werkBon.Status = nieuweStatus;
            werkBon.BijgewerktOp = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }

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

            var werkBon = taak.WerkBon;

            bool allesKlaar = werkBon.Taken.All(t => t.Resource == "Voltooid");

            if (allesKlaar)
                werkBon.Status = WerkBonStatus.Afgewerkt;

            await db.SaveChangesAsync();
        }

        private static bool IsValidTransition(WerkBonStatus from, WerkBonStatus to)
        {
            return (from, to) switch
            {
                (WerkBonStatus.Nieuw, WerkBonStatus.InPlanning) => true,
                (WerkBonStatus.InPlanning, WerkBonStatus.InUitvoering) => true,
                (WerkBonStatus.InUitvoering, WerkBonStatus.Afgewerkt) => true,
                (_, WerkBonStatus.Geannuleerd) => true,
                _ => false
            };
        }

    }
}
