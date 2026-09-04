using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// Offertenummer moet doorlopend blijven, ook als de hoogst-genummerde offerte gearchiveerd
/// (= verwijderd uit Offertes) is — anders zou een nieuwe offerte een al gebruikt nummer
/// hergebruiken.
/// </summary>
public class OfferteNummeringTests
{
    private static PooledDbContextFactory<AppDbContext> CreateFactory() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Eerste_nummer_is_1_bij_lege_database()
    {
        var factory = CreateFactory();
        await using var db = await factory.CreateDbContextAsync();

        var volgende = await OfferteNummering.VolgendeAsync(db);

        Assert.Equal(1, volgende);
    }

    [Fact]
    public async Task Volgt_op_het_hoogste_actieve_nummer()
    {
        var factory = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Offertes.Add(new Offerte { OfferteNummer = 1, Datum = DateTime.Today });
            db.Offertes.Add(new Offerte { OfferteNummer = 5, Datum = DateTime.Today });
            await db.SaveChangesAsync();
        }

        await using var db2 = await factory.CreateDbContextAsync();
        var volgende = await OfferteNummering.VolgendeAsync(db2);

        Assert.Equal(6, volgende);
    }

    [Fact]
    public async Task Houdt_rekening_met_gearchiveerde_nummers_ook_al_bestaat_de_actieve_offerte_niet_meer()
    {
        // Simuleert: offerte #7 was de hoogste, is gearchiveerd (dus verwijderd uit Offertes).
        // Een nieuwe offerte mag nooit opnieuw #7 (of lager) krijgen — anders duiken er twee
        // offertes met hetzelfde nummer op (één actief, één in het archief).
        var factory = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Offertes.Add(new Offerte { OfferteNummer = 3, Datum = DateTime.Today });
            db.OfferteArchieven.Add(new OfferteArchief
            {
                OfferteNummer = 7,
                OrigineleOfferteId = 99,
                OfferteDatum = DateTime.Today,
                Jaar = DateTime.Today.Year,
                KlantNaam = "(geen klant)",
                StatusOpMoment = "Concept",
                GearchiveerdOp = DateTime.UtcNow,
                Snapshot = "{}"
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = await factory.CreateDbContextAsync();
        var volgende = await OfferteNummering.VolgendeAsync(db2);

        Assert.Equal(8, volgende);
    }
}
