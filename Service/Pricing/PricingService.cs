using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
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

        var parameters = await LoadParametersAsync();

        var offerte = await db.Offertes
            .Include(x => x.Regels).ThenInclude(r => r.TypeLijst)
            .Include(x => x.Regels).ThenInclude(r => r.Glas)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout1)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout2)
            .Include(x => x.Regels).ThenInclude(r => r.DiepteKern)
            .Include(x => x.Regels).ThenInclude(r => r.Opkleven)
            .Include(x => x.Regels).ThenInclude(r => r.Rug)
            .FirstAsync(x => x.Id == offerteId);

        ApplyResult(offerte, Calculate(offerte, parameters));

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

    public async Task BerekenAsync(Offerte offerte)
    {
        ArgumentNullException.ThrowIfNull(offerte);

        var parameters = await LoadParametersAsync();
        ApplyResult(offerte, Calculate(offerte, parameters));
    }

    private async Task<PricingParameters> LoadParametersAsync()
    {
        return new PricingParameters(
            await _settingsProvider.GetUurloonAsync(),
            await _settingsProvider.GetBtwPercentAsync(),
            await _settingsProvider.GetDefaultPrijsPerMeterAsync(),
            await _settingsProvider.GetDefaultWinstFactorAsync(),
            await _settingsProvider.GetDefaultAfvalPercentageAsync());
    }

    private PricingResult Calculate(Offerte offerte, PricingParameters parameters) =>
        _engine.Calculate(
            offerte,
            parameters.Uurloon,
            parameters.BtwPercent,
            parameters.DefaultPrijsPerMeter,
            parameters.DefaultWinstFactor,
            parameters.DefaultAfvalPercentage);

    private static void ApplyResult(Offerte offerte, PricingResult result)
    {
        var regels = offerte.Regels.ToList();
        if (regels.Count != result.Regels.Count)
        {
            throw new InvalidOperationException("Pricing result does not match the number of offerte regels.");
        }

        for (var i = 0; i < regels.Count; i++)
        {
            var regel = regels[i];
            var regelResult = result.Regels[i];
            regel.TotaalExcl = regelResult.TotaalExcl;
            regel.SubtotaalExBtw = regelResult.SubtotaalExBtw;
            regel.BtwBedrag = regelResult.BtwBedrag;
            regel.TotaalInclBtw = regelResult.TotaalInclBtw;
        }

        offerte.SubtotaalExBtw = result.SubtotaalExBtw;
        offerte.BtwBedrag = result.BtwBedrag;
        offerte.TotaalInclBtw = result.TotaalInclBtw;
        offerte.VoorschotBedrag = result.VoorschotBedrag;
    }

    private sealed record PricingParameters(
        decimal Uurloon,
        decimal BtwPercent,
        decimal DefaultPrijsPerMeter,
        decimal DefaultWinstFactor,
        decimal DefaultAfvalPercentage);
}
