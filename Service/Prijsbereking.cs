using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Services.Pricing;

public sealed class PricingService : IPricingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PricingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task BerekenAsync(int offerteId)
    {
        Console.WriteLine("=== START BEREKEN ===");
        Console.WriteLine($"OfferteId: {offerteId}");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var inst = await db.Instellingen.ToDictionaryAsync(x => x.Sleutel, x => x.Waarde);
        decimal uurloon = decimal.TryParse(inst.GetValueOrDefault("Uurloon"), out var u) ? u : 45m;
        decimal btwPct = decimal.TryParse(inst.GetValueOrDefault("BtwPercent"), out var b) ? b : 21m;
        decimal btwFactor = btwPct / 100m;

        Console.WriteLine($"Uurloon: {uurloon}");
        Console.WriteLine($"BTW%: {btwPct}");

        var o = await db.Offertes
            .Include(x => x.Regels).ThenInclude(r => r.TypeLijst)
            .Include(x => x.Regels).ThenInclude(r => r.Glas)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout1)
            .Include(x => x.Regels).ThenInclude(r => r.PassePartout2)
            .Include(x => x.Regels).ThenInclude(r => r.DiepteKern)
            .Include(x => x.Regels).ThenInclude(r => r.Opkleven)
            .Include(x => x.Regels).ThenInclude(r => r.Rug)
            .FirstAsync(x => x.Id == offerteId);

        Console.WriteLine($"Aantal regels: {o.Regels.Count}");

        decimal totaalRegelsEx = 0m;

        foreach (var r in o.Regels)
        {
            Console.WriteLine($"-- Regel {r.Id}");

            decimal SurfaceM2()
            {
                var bw = r.InlegBreedteCm ?? r.BreedteCm;
                var bh = r.InlegHoogteCm ?? r.HoogteCm;
                return (bw * bh) / 10_000m;
            }

            decimal CalcOpt(AfwerkingsOptie? opt)
            {
                if (opt is null) return 0m;

                var m2 = SurfaceM2();
                var kost = opt.KostprijsPerM2 * m2 + opt.VasteKost;
                var afval = kost * (opt.AfvalPercentage / 100m);
                var arbeid = (opt.WerkMinuten / 60m) * uurloon;

                return Math.Round((kost + afval) * (1m + opt.WinstMarge) + arbeid, 2);
            }

            decimal lijstPrijs = r.AfgesprokenPrijsExcl ?? 0m;

            if (!r.AfgesprokenPrijsExcl.HasValue && r.TypeLijst is not null)
            {
                var perimMm =
                    (r.BreedteCm + r.HoogteCm) * 2m * 10m
                    + (r.TypeLijst.BreedteCm * 10m);

                var lengteM = perimMm / 1000m;

                var kost = r.TypeLijst.PrijsPerMeter * lengteM;
                var afval = kost * (r.TypeLijst.AfvalPercentage / 100m);
                var arbeid = (r.TypeLijst.WerkMinuten / 60m) * uurloon;

                lijstPrijs = Math.Round(
                    (kost + afval) * (1m + r.TypeLijst.WinstMargeFactor)
                    + r.TypeLijst.VasteKost
                    + arbeid,
                    2);
            }

            var optiesEx =
                CalcOpt(r.Glas) +
                CalcOpt(r.PassePartout1) +
                CalcOpt(r.PassePartout2) +
                CalcOpt(r.DiepteKern) +
                CalcOpt(r.Opkleven) +
                CalcOpt(r.Rug);

            decimal lineEx = lijstPrijs + optiesEx;
            lineEx += (r.ExtraWerkMinuten / 60m) * uurloon;
            lineEx += r.ExtraPrijs;
            lineEx -= r.Korting;
            lineEx = Math.Max(0m, lineEx) * Math.Max(1, r.AantalStuks);

            Console.WriteLine($"RegelExcl: {lineEx}");

            r.TotaalExcl = lineEx;
            r.SubtotaalExBtw = lineEx;

            totaalRegelsEx += lineEx;
        }

        Console.WriteLine($"Totaal regels excl: {totaalRegelsEx}");

        var kortingExcl = totaalRegelsEx * (o.KortingPct / 100m);
        var meerPrijsIncl = o.MeerPrijsIncl;
        var meerPrijsExcl = meerPrijsIncl / (1m + btwFactor);
        var meerPrijsBtw = meerPrijsIncl - meerPrijsExcl;

        var nieuwSubtotaalEx =
            totaalRegelsEx
            - kortingExcl
            + meerPrijsExcl;

        nieuwSubtotaalEx = Math.Max(0m, nieuwSubtotaalEx);

        var btwRegels = nieuwSubtotaalEx * btwFactor;

        var totaalIncl =
            nieuwSubtotaalEx
            + btwRegels
            + meerPrijsBtw;

        o.SubtotaalExBtw = Math.Round(nieuwSubtotaalEx, 2);
        o.BtwBedrag = Math.Round(btwRegels + meerPrijsBtw, 2);
        o.TotaalInclBtw = Math.Round(totaalIncl, 2);

        Console.WriteLine($"Totaal Incl: {o.TotaalInclBtw}");

        if (o.VoorschotBedrag > o.TotaalInclBtw)
            o.VoorschotBedrag = o.TotaalInclBtw;

        await db.SaveChangesAsync();

        Console.WriteLine("=== EINDE BEREKEN ===");
    }
}
