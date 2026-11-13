using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Feedback Veerle (12/11): op de weeklijst ontbrak de naam van een klant zonder
/// achternaam, en klanten met dezelfde achternaam vielen onder één kop.</summary>
public class WeekLijstKlantTests
{
    [Theory]
    [InlineData("Veerle", "Gobien", "GOBIEN Veerle")]
    [InlineData("Voetbalploeg KFC Aarschot", "", "VOETBALPLOEG KFC AARSCHOT")]
    [InlineData("", "Peeters", "PEETERS")]
    [InlineData(" ", " ", "ONBEKEND")]
    public void Kop_toont_altijd_een_naam(string voornaam, string achternaam, string verwacht)
        => Assert.Equal(verwacht, WeekWerkLijstViewModel.KlantKop(new Klant { Voornaam = voornaam, Achternaam = achternaam }));

    [Fact]
    public async Task Elke_klant_een_eigen_kop_met_naam()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var f = scope.Factory;
        var maandag = new DateTime(2026, 11, 9);   // week 46
        await using (var db = await f.CreateDbContextAsync())
        {
            void Taak(Klant klant, string titel)
            {
                var offerte = new Offerte { OfferteNummer = Random.Shared.Next(1000, 9999), Datum = maandag, Status = OfferteStatus.Goedgekeurd, Klant = klant };
                var bon = new WerkBon { Offerte = offerte, Status = WerkBonStatus.Gepland };
                db.WerkTaken.Add(new WerkTaak
                {
                    WerkBon = bon, Omschrijving = titel, DuurMinuten = 60,
                    GeplandVan = maandag.AddHours(10), GeplandTot = maandag.AddHours(11)
                });
            }
            Taak(new Klant { Voornaam = "Voetbalploeg", Achternaam = "" }, "Foto voetbalploeg");
            Taak(new Klant { Voornaam = "Jan", Achternaam = "Peeters" }, "Litho");
            Taak(new Klant { Voornaam = "An", Achternaam = "Peeters" }, "Aquarel");
            await db.SaveChangesAsync();
        }

        var vm = new WeekWerkLijstViewModel(f, new QuadroApp.Service.WorkflowService(
            f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, new TestToastService()));
        await vm.InitializeAsync(2026, 46);

        Assert.Equal(new[] { "PEETERS An", "PEETERS Jan", "VOETBALPLOEG" }, vm.Blocks.Select(b => b.KlantNaam).ToArray());
        Assert.All(vm.Blocks, b => Assert.Single(b.Items));
    }
}
