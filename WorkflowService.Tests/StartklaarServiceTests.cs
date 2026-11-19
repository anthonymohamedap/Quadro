using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Security;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>"Startklaar maken" (start op 1 januari 2027): testgegevens weg, basisgegevens blijven.</summary>
[Collection("StaticAuthState")]
public class StartklaarServiceTests
{
    private static async Task VulAsync(IDbContextFactory<AppDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();

        // ── Blijft ──
        var leverancier = new Leverancier { Naam = "ICO" };
        var lijst = new TypeLijst
        {
            Artikelnummer = "0303/132", Levcode = "ICO", Leverancier = leverancier, PrijsPerMeter = 10m,
            VoorraadMeter = 25.5m, GereserveerdeVoorraadMeter = 4m
        };
        var groep = new AfwerkingsGroep { Code = 'G', Naam = "Glas" };
        db.AfwerkingsOpties.Add(new AfwerkingsOptie { AfwerkingsGroep = groep, Naam = "Helder glas", Volgnummer = '1' });
        db.KantKlaarKaders.Add(new KantKlaarKader { Naam = "C2", PrijsPerStukExcl = 37.19m });
        db.Gebruikers.Add(new Gebruiker { GebruikersNaam = "veerle", VolledigeNaam = "Veerle", WachtwoordHash = "x", Rol = GebruikersRol.Admin });
        db.Instellingen.Add(new Instelling { Sleutel = "Test.Instelling", Waarde = "1" });
        db.GeblokkeerDagen.Add(new GeblokkeerdeDag { Datum = new DateTime(2027, 1, 1), Reden = "Nieuwjaar" });

        // ── Weg ──
        var klant = new Klant { Voornaam = "Test", Achternaam = "Klant" };
        var gearchiveerdeKlant = new Klant { Voornaam = "Oud", Achternaam = "Archief", IsGearchiveerd = true };
        db.Klanten.Add(gearchiveerdeKlant);
        var regel = new OfferteRegel { Titel = "Trouwfoto", AantalStuks = 1, BreedteCm = 50, HoogteCm = 70, TypeLijst = lijst };
        var offerte = new Offerte
        {
            OfferteNummer = 412, Datum = new DateTime(2026, 11, 1), Status = OfferteStatus.Goedgekeurd,
            Klant = klant, Regels = { regel }
        };
        var werkBon = new WerkBon { Offerte = offerte, Status = WerkBonStatus.Gepland };
        var bestelling = new LeverancierBestelling { Leverancier = leverancier, BestelNummer = "ICO-1" };
        var bestelLijn = new LeverancierBestelLijn { LeverancierBestelling = bestelling, TypeLijst = lijst, WerkBon = werkBon, AantalMeterBesteld = 3m };
        var taak = new WerkTaak
        {
            WerkBon = werkBon, OfferteRegel = regel, Omschrijving = "Trouwfoto", DuurMinuten = 60,
            GeplandVan = new DateTime(2026, 11, 20, 9, 0, 0), GeplandTot = new DateTime(2026, 11, 20, 10, 0, 0),
            LeverancierBestelLijn = bestelLijn
        };
        db.WerkTaken.Add(taak);
        var bon = new Bestelbon
        {
            Offerte = offerte, WerkBon = werkBon, Jaar = 2026, VolgNr = 19, BestelbonNummer = "2026-19",
            KlantNaam = "Test Klant", BestelbonDatum = new DateTime(2026, 11, 1), VervalDatum = new DateTime(2026, 12, 1),
            Lijnen = { new BestelbonLijn { Omschrijving = "Kader 50x70" } }
        };
        db.Bestelbonnen.Add(bon);
        db.Ontvangsten.Add(new Ontvangst { Offerte = offerte, Soort = OntvangstSoort.Voorschot, BedragIncl = 50m, Datum = new DateTime(2026, 11, 1) });
        db.Ontvangsten.Add(new Ontvangst { Soort = OntvangstSoort.Winkelverkoop, Omschrijving = "Wenskaart", BedragIncl = 3.5m, Datum = new DateTime(2026, 11, 2) });
        db.VoorraadMutaties.Add(new VoorraadMutatie { TypeLijst = lijst, AantalMeter = 2m, WerkBon = werkBon, WerkTaak = taak });
        db.VoorraadAlerts.Add(new VoorraadAlert { TypeLijst = lijst });
        db.OfferteArchieven.Add(new OfferteArchief { OrigineleOfferteId = 99, OfferteNummer = 411, KlantNaam = "Oud", OfferteDatum = new DateTime(2026, 10, 1), Jaar = 2026 });
        db.ImportSessions.Add(new ImportSession { StartedAt = DateTime.UtcNow, EntityName = "Klant", FileName = "klanten.xlsx" });
        db.FoutLogs.Add(new FoutLog { Tijdstip = DateTime.UtcNow, Bron = "Test", Melding = "test", ExceptionType = "X", Stacktrace = "x", Computernaam = "pc", AppVersie = "1", Hash = Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync();   // schrijft ook auditregels
    }

    private static StartklaarService Sut(IDbContextFactory<AppDbContext> f, bool admin = true)
    {
        var auth = new TestAuthService { AlleRechten = admin };
        return new StartklaarService(f, auth);
    }

    [Fact]
    public async Task Telt_zonder_iets_te_wijzigen()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await VulAsync(scope.Factory);

        var overzicht = await Sut(scope.Factory).TelAsync();

        Assert.Equal(2, overzicht.Weg.Single(r => r.Wat == "Klanten").Aantal);   // ook de gearchiveerde
        Assert.Equal(1, overzicht.Weg.Single(r => r.Wat == "Offertes").Aantal);
        Assert.Equal(2, overzicht.Weg.Single(r => r.Wat.StartsWith("Betalingen")).Aantal);
        Assert.True(overzicht.Weg.Single(r => r.Wat == "Auditlog").Aantal > 0);
        Assert.Equal(1, overzicht.Blijft.Single(r => r.Wat.StartsWith("Lijsten")).Aantal);

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Offertes.CountAsync());
    }

    [Fact]
    public async Task Verwijdert_testgegevens_en_laat_basisgegevens_staan()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await VulAsync(scope.Factory);

        await Sut(scope.Factory).VoerUitAsync();

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Klanten.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Offertes.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.OfferteRegels.CountAsync());
        Assert.Equal(0, await db.WerkBonnen.CountAsync());
        Assert.Equal(0, await db.WerkTaken.CountAsync());
        Assert.Equal(0, await db.Bestelbonnen.CountAsync());
        Assert.Equal(0, await db.BestelbonLijnen.CountAsync());
        Assert.Equal(0, await db.Ontvangsten.CountAsync());
        Assert.Equal(0, await db.OfferteArchieven.CountAsync());
        Assert.Equal(0, await db.LeverancierBestellingen.CountAsync());
        Assert.Equal(0, await db.LeverancierBestelLijnen.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.VoorraadMutaties.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.VoorraadAlerts.CountAsync());
        Assert.Equal(0, await db.ImportSessions.CountAsync());
        Assert.Equal(0, await db.FoutLogs.CountAsync());

        // Eén auditregel: wie het deed.
        var audit = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal("Startklaar gemaakt", audit.Actie);

        // Basisgegevens blijven; voorraad blijft, reservaties weg.
        var lijst = Assert.Single(await db.TypeLijsten.ToListAsync());
        Assert.Equal(25.5m, lijst.VoorraadMeter);
        Assert.Equal(0m, lijst.GereserveerdeVoorraadMeter);
        Assert.Equal(1, await db.AfwerkingsOpties.CountAsync());
        Assert.Equal(1, await db.KantKlaarKaders.CountAsync());
        Assert.Equal(1, await db.Leveranciers.CountAsync());
        Assert.Equal(1, await db.Gebruikers.CountAsync());
        Assert.Equal(1, await db.GeblokkeerDagen.CountAsync());
        Assert.True(await db.Instellingen.AnyAsync(i => i.Sleutel == "Test.Instelling"));
    }

    [Fact]
    public async Task Daarna_begint_de_nummering_opnieuw()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await VulAsync(scope.Factory);
        await Sut(scope.Factory).VoerUitAsync();

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await OfferteNummering.VolgendeAsync(db));

        var klant = new Klant { Voornaam = "Eerste", Achternaam = "Klant" };
        db.Klanten.Add(klant);
        await db.SaveChangesAsync();
        Assert.Equal(1, klant.Id);   // interne teller opnieuw bij 1
    }

    [Fact]
    public async Task Enkel_de_beheerder_mag_het()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await VulAsync(scope.Factory);

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => Sut(scope.Factory, admin: false).VoerUitAsync());

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Offertes.CountAsync());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("startklaar", false)]
    [InlineData("START", false)]
    [InlineData("STARTKLAAR", true)]
    [InlineData(" STARTKLAAR ", true)]
    public void Knop_werkt_pas_na_het_intypen_van_STARTKLAAR(string ingetypt, bool kan)
    {
        var vm = new QuadroApp.ViewModels.Gebruikers.StartklaarViewModel(
            new StartklaarService(DbFactoryBuilder.CreateInMemoryFactory(), new TestAuthService()), new TestToastService())
        {
            Bevestiging = ingetypt
        };
        Assert.Equal(kan, vm.KanUitvoeren);
        Assert.Equal(kan, vm.UitvoerenCommand.CanExecute(null));
    }
}
