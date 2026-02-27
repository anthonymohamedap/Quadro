using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class OfferteWorkflowService : IOfferteWorkflowService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IWerkBonWorkflowService _werkBonWorkflow;

        public OfferteWorkflowService(
            IDbContextFactory<AppDbContext> factory,
            IWerkBonWorkflowService werkBonWorkflow)
        {
            _factory = factory;
            _werkBonWorkflow = werkBonWorkflow;
        }

        public async Task<int> BevestigAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var offerte = await db.Offertes
                .Include(o => o.WerkBon)
                .Include(o => o.Regels) // handig als je straks taken wil maken
                .FirstOrDefaultAsync(o => o.Id == offerteId);

            if (offerte == null)
                throw new InvalidOperationException("Offerte niet gevonden.");

            if (offerte.Status != OfferteStatus.Nieuw)
                throw new InvalidOperationException("Enkel nieuwe offertes kunnen bevestigd worden.");

            // ✅ voorkom dubbel
            if (offerte.WerkBon != null)
                return offerte.WerkBon.Id;

            offerte.Status = OfferteStatus.Bevestigd;

            var werkBon = new WerkBon
            {
                OfferteId = offerteId,
                TotaalPrijsIncl = offerte.TotaalInclBtw,
                Status = WerkBonStatus.Nieuw
            };

            db.WerkBonnen.Add(werkBon);

            // (optioneel) hier kan je meteen WerkTaken maken per regel

            await db.SaveChangesAsync();

            return werkBon.Id;
        }

        public async Task AnnuleerAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var offerte = await db.Offertes.FindAsync(offerteId);
            if (offerte == null)
                throw new InvalidOperationException("Offerte niet gevonden.");

            offerte.Status = OfferteStatus.Geannuleerd;

            await db.SaveChangesAsync();
        }
    }
}
