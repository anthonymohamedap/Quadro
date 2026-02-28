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
        private readonly IWorkflowService _workflow;

        public OfferteWorkflowService(IDbContextFactory<AppDbContext> factory, IWorkflowService workflow)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        public async Task<int> BevestigAsync(int offerteId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var huidigeStatus = await db.Offertes
                .Where(o => o.Id == offerteId)
                .Select(o => (OfferteStatus?)o.Status)
                .FirstOrDefaultAsync();

            if (huidigeStatus is null)
                throw new InvalidOperationException("Offerte niet gevonden.");

            if (huidigeStatus == OfferteStatus.Concept)
                await _workflow.ChangeOfferteStatusAsync(offerteId, OfferteStatus.Verzonden);

            await _workflow.ChangeOfferteStatusAsync(offerteId, OfferteStatus.Goedgekeurd);

            var werkBonId = await db.WerkBonnen
                .Where(w => w.OfferteId == offerteId)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();

            if (werkBonId == 0)
            {
                throw new InvalidOperationException("Werkbon niet gevonden.");
            }

            return werkBonId;
        }

        public Task AnnuleerAsync(int offerteId) =>
            _workflow.ChangeOfferteStatusAsync(offerteId, OfferteStatus.Geannuleerd);

        public Task ChangeOfferteStatusAsync(int offerteId, OfferteStatus newStatus) =>
            _workflow.ChangeOfferteStatusAsync(offerteId, newStatus);
    }
}
