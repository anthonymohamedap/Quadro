using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-67 — overzicht betalingen: groeperen per dag × betaalwijze, bonnummers, periode.</summary>
public class BetalingsOverzichtTests
{
    private static BetalingsOverzichtRegel Regel(int id, DateTime datum, Betaalwijze wijze, decimal bedrag, string bon = "") =>
        new(id, datum, OntvangstSoort.Betaling, wijze, bedrag, bon, null, null);

    [Fact]
    public void Bouw_groepeert_per_dag_en_telt_per_betaalwijze()
    {
        var d1 = new DateTime(2026, 9, 1); // dinsdag
        var d2 = new DateTime(2026, 9, 2); // woensdag

        var overzicht = BetalingsOverzicht.Bouw(d1, d2, new[]
        {
            Regel(1, d1, Betaalwijze.Kontant, 20m, "B-1"),
            Regel(2, d1, Betaalwijze.Kontant, 5.50m),
            Regel(3, d1, Betaalwijze.Bancontact, 100m, "B-2"),
            Regel(4, d2, Betaalwijze.Storting, 250m, "B-3"),
            Regel(5, d2.AddDays(1), Betaalwijze.Visa, 999m) // buiten de periode
        });

        Assert.Equal(2, overzicht.Dagen.Count);

        var dag1 = overzicht.Dagen[0];
        Assert.Equal(d1, dag1.Datum);
        Assert.Equal("di", dag1.DagNaam);
        Assert.Equal(25.50m, dag1.Kontant);
        Assert.Equal(100m, dag1.Bancontact);
        Assert.Equal(125.50m, dag1.Totaal);
        // Regels met bonnummer eerst, winkelverkopen (blanco bon) achteraan.
        Assert.Equal(new[] { "B-1", "B-2", "" }, dag1.Regels.Select(r => r.Bon));

        Assert.Equal("wo", overzicht.Dagen[1].DagNaam);
        Assert.Equal(25.50m, overzicht.Kontant);
        Assert.Equal(250m, overzicht.Storting);
        Assert.Equal(0m, overzicht.Visa);
        Assert.Equal(375.50m, overzicht.Totaal);
        Assert.Equal(4, overzicht.AantalOntvangsten);
    }

    [Fact]
    public void Kolomvolgorde_volgt_de_oude_kassa()
    {
        Assert.Equal(
            new[] { Betaalwijze.Kontant, Betaalwijze.Cheque, Betaalwijze.Visa, Betaalwijze.Bancontact, Betaalwijze.Proton, Betaalwijze.Storting },
            BetaalwijzeTotalen.KolomVolgorde);
    }

    [Fact]
    public async Task Service_toont_bonnummer_voor_betaling_en_voorschot_en_blanco_voor_winkelverkoop()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var dag = new DateTime(2026, 9, 15);

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var metBon = new Offerte { Datum = dag, OfferteNummer = 41, Klant = new Klant { Voornaam = "An", Achternaam = "Bon" }, TotaalInclBtw = 300m };
            var zonderBon = new Offerte { Datum = dag, OfferteNummer = 42, Klant = new Klant { Voornaam = "Bert", Achternaam = "Nog" }, TotaalInclBtw = 200m };
            db.Offertes.AddRange(metBon, zonderBon);
            await db.SaveChangesAsync();

            var factuur = new Factuur
            {
                OfferteId = metBon.Id, Jaar = 2026, VolgNr = 12, FactuurNummer = "2026-012",
                KlantNaam = "An Bon", Status = FactuurStatus.KlaarVoorExport, TotaalInclBtw = 300m
            };
            db.Facturen.Add(factuur);
            await db.SaveChangesAsync();

            db.Ontvangsten.AddRange(
                // Voorschot enkel aan de offerte gekoppeld → toont het nummer van de bestelbon.
                new Ontvangst { Soort = OntvangstSoort.Voorschot, OfferteId = metBon.Id, Datum = dag, Betaalwijze = Betaalwijze.Kontant, BedragIncl = 100m },
                // Betaling op de bestelbon.
                new Ontvangst { Soort = OntvangstSoort.Betaling, FactuurId = factuur.Id, OfferteId = metBon.Id, Datum = dag, Betaalwijze = Betaalwijze.Bancontact, BedragIncl = 200m },
                // Voorschot zonder bestelbon → offertenummer.
                new Ontvangst { Soort = OntvangstSoort.Voorschot, OfferteId = zonderBon.Id, Datum = dag, Betaalwijze = Betaalwijze.Visa, BedragIncl = 50m },
                // Winkelverkoop → geen bon.
                new Ontvangst { Soort = OntvangstSoort.Winkelverkoop, Datum = dag, Betaalwijze = Betaalwijze.Kontant, Omschrijving = "Wenskaart", Aantal = 1, PrijsPerStukIncl = 3.50m, BedragIncl = 3.50m },
                // Buiten de periode.
                new Ontvangst { Soort = OntvangstSoort.Winkelverkoop, Datum = dag.AddMonths(-1), Betaalwijze = Betaalwijze.Kontant, Omschrijving = "Oud", Aantal = 1, PrijsPerStukIncl = 10m, BedragIncl = 10m });
            await db.SaveChangesAsync();
        }

        var sut = new BetalingsOverzichtService(scope.Factory);
        var overzicht = await sut.GetOverzichtAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));

        var d = Assert.Single(overzicht.Dagen);
        Assert.Equal(4, d.Regels.Count);

        Assert.All(d.Regels.Where(r => r.Betaalwijze != Betaalwijze.Visa && r.Soort != OntvangstSoort.Winkelverkoop),
            r => { Assert.Equal("2026-012", r.Bon); Assert.Equal(2026, r.Jaar); });
        Assert.Equal("offerte 42", d.Regels.Single(r => r.Betaalwijze == Betaalwijze.Visa).Bon);
        Assert.Equal(string.Empty, d.Regels.Single(r => r.Soort == OntvangstSoort.Winkelverkoop).Bon);

        Assert.Equal(103.50m, overzicht.Kontant);
        Assert.Equal(200m, overzicht.Bancontact);
        Assert.Equal(50m, overzicht.Visa);
        Assert.Equal(353.50m, overzicht.Totaal);
    }

    [Fact]
    public async Task Service_verwisselt_van_en_tot_als_ze_omgekeerd_zijn()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            db.Ontvangsten.Add(new Ontvangst { Soort = OntvangstSoort.Winkelverkoop, Datum = new DateTime(2026, 9, 10), Betaalwijze = Betaalwijze.Proton, Omschrijving = "x", Aantal = 1, PrijsPerStukIncl = 7m, BedragIncl = 7m });
            await db.SaveChangesAsync();
        }

        var overzicht = await new BetalingsOverzichtService(scope.Factory)
            .GetOverzichtAsync(new DateTime(2026, 9, 30), new DateTime(2026, 9, 1));

        Assert.Equal(new DateTime(2026, 9, 1), overzicht.Van);
        Assert.Equal(7m, overzicht.Proton);
    }
}
