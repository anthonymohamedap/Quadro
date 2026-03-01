using QuadroApp.Model.DB;
using System;

namespace QuadroApp.Service.Pricing;

public sealed class PricingEngine
{
    public PricingResult Calculate(Offerte offerte, decimal uurloon, decimal btwPercent)
    {
        var btwFactor = btwPercent / 100m;
        var regelResults = new System.Collections.Generic.List<PricingRegelResult>();

        decimal totaalRegelsEx = 0m;

        foreach (var r in offerte.Regels)
        {
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
                lijstPrijs = CalculateLijstPrijsExcl(r.TypeLijst, r.BreedteCm, r.HoogteCm, uurloon);
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

            var btwBedrag = Math.Round(lineEx * btwFactor, 2);
            var incl = Math.Round(lineEx + btwBedrag, 2);

            regelResults.Add(new PricingRegelResult
            {
                RegelId = r.Id,
                TotaalExcl = lineEx,
                SubtotaalExBtw = lineEx,
                BtwBedrag = btwBedrag,
                TotaalInclBtw = incl
            });

            totaalRegelsEx += lineEx;
        }

        var kortingExcl = totaalRegelsEx * (offerte.KortingPct / 100m);
        var meerPrijsIncl = offerte.MeerPrijsIncl;
        var meerPrijsExcl = meerPrijsIncl / (1m + btwFactor);
        var meerPrijsBtw = meerPrijsIncl - meerPrijsExcl;

        var nieuwSubtotaalEx = totaalRegelsEx - kortingExcl + meerPrijsExcl;
        nieuwSubtotaalEx = Math.Max(0m, nieuwSubtotaalEx);

        var btwRegels = nieuwSubtotaalEx * btwFactor;
        var totaalIncl = nieuwSubtotaalEx + btwRegels + meerPrijsBtw;

        var totaalInclRound = Math.Round(totaalIncl, 2);

        var output = new PricingResult
        {
            SubtotaalExBtw = Math.Round(nieuwSubtotaalEx, 2),
            BtwBedrag = Math.Round(btwRegels + meerPrijsBtw, 2),
            TotaalInclBtw = totaalInclRound,
            VoorschotBedrag = offerte.VoorschotBedrag > totaalInclRound ? totaalInclRound : offerte.VoorschotBedrag
        };

        output.Regels.AddRange(regelResults);
        return output;
    }

    public static decimal CalculateLijstPrijsExcl(TypeLijst lijst, decimal breedteCm, decimal hoogteCm, decimal uurloon)
    {
        var perimMm = (breedteCm + hoogteCm) * 2m * 10m + (lijst.BreedteCm * 10m);
        var lengteM = perimMm / 1000m;

        var kost = lijst.PrijsPerMeter * lengteM;
        var afval = kost * (lijst.AfvalPercentage / 100m);
        var arbeid = (lijst.WerkMinuten / 60m) * uurloon;

        return Math.Round(
            (kost + afval) * (1m + lijst.WinstMargeFactor)
            + lijst.VasteKost
            + arbeid,
            2);
    }
}
