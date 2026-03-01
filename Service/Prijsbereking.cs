using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Pricing;

public sealed class PricingService : IPricingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IPricingSettingsProvider _settingsProvider;
    private readonly PricingEngine _engine;
    private readonly ILogger<PricingService> _logger;

    public PricingService(
        IDbContextFactory<AppDbContext> dbFactory,
        IPricingSettingsProvider settingsProvider,
        PricingEngine engine,
        ILogger<PricingService> logger)
    {
        _dbFactory = dbFactory;
        _settingsProvider = settingsProvider;
        _engine = engine;
        _logger = logger;
    }

    public async Task BerekenAsync(int offerteId)
    {
        var sw = Stopwatch.StartNew();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var uurloon = await _settingsProvider.GetUurloonAsync();
        var btwPct = await _settingsProvider.GetBtwPercentAsync();

        var offerte = await db.Offertes
            .Include(x => x.Regels).ThenInclude(r => r.TypeLijst)
            .Include(x => x.Regels).ThenInclude(r => r.Glas)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout1)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout2)
            .Include(x => x.Regels).ThenInclude(r => r.DiepteKern)
            .Include(x => x.Regels).ThenInclude(r => r.Opkleven)
            .Include(x => x.Regels).ThenInclude(r => r.Rug)
            .FirstAsync(x => x.Id == offerteId);

        var result = _engine.Calculate(offerte, uurloon, btwPct);

        foreach (var regel in offerte.Regels)
        {
            var regelResult = result.Regels.FirstOrDefault(x => x.RegelId == regel.Id);
            if (regelResult is null)
                continue;

            regel.TotaalExcl = regelResult.TotaalExcl;
            regel.SubtotaalExBtw = regelResult.SubtotaalExBtw;
            regel.BtwBedrag = regelResult.BtwBedrag;
            regel.TotaalInclBtw = regelResult.TotaalInclBtw;
        }

        offerte.SubtotaalExBtw = result.SubtotaalExBtw;
        offerte.BtwBedrag = result.BtwBedrag;
        offerte.TotaalInclBtw = result.TotaalInclBtw;
        offerte.VoorschotBedrag = result.VoorschotBedrag;

        await db.SaveChangesAsync();

        sw.Stop();
        _logger.LogInformation(
            "Pricing completed for OfferteId={OfferteId}. Regels={RegelCount}, SubtotaalEx={SubtotaalEx}, Btw={Btw}, TotaalIncl={TotaalIncl}, ElapsedMs={ElapsedMs}",
            offerteId,
            offerte.Regels.Count,
            offerte.SubtotaalExBtw,
            offerte.BtwBedrag,
            offerte.TotaalInclBtw,
            sw.ElapsedMilliseconds);
    }
}
