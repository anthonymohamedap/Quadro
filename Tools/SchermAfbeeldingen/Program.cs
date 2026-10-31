// Rendert schermen van QuadroApp buiten beeld (Avalonia.Headless + Skia) naar PNG, met testgegevens.
//
//   dotnet run --project Tools/SchermAfbeeldingen -- <uitvoermap> [scenario ...]
//
// Zonder scenario's worden ze allemaal gerenderd. Een nieuw scherm toevoegen = een scenario
// toevoegen aan `Scenarios` hieronder (seed wat het scherm nodig heeft, bouw de ViewModel op
// zoals de app, en roep Schermen.RenderAsync aan). Zie CLAUDE.md.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Verkoop;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using QuadroApp.Views;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Tests.TestInfrastructure;

var nl = System.Globalization.CultureInfo.GetCultureInfo("nl-BE");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = nl;
System.Globalization.CultureInfo.CurrentCulture = nl;

var uitvoer = args.Length > 0 ? args[0] : "schermafbeeldingen";
var gekozen = args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase);
Directory.CreateDirectory(uitvoer);

AppBuilder.Configure<RenderApp>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// De app wacht op de UI-thread; die moet hier zelf verwerkt worden zolang er gerenderd wordt.
var job = Schermen.AllesAsync(uitvoer, gekozen);
while (!job.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
job.GetAwaiter().GetResult();

static class Schermen
{
    static readonly Dictionary<string, Func<IDbContextFactory<AppDbContext>, Task<Control>>> Scenarios = new()
    {
        ["offerte-1-concept"] = f => OfferteAsync(f, 419),
        ["offerte-1b-verzonden"] = f => OfferteAsync(f, 418),
        ["offerte-2-bevestigd"] = f => OfferteAsync(f, 412),
        ["offerte-3-afgerond"] = f => OfferteAsync(f, 398),
        ["offertelijst"] = f => OffertelijstAsync(f, VerkoopFase.OpenOffertes),
        ["offertelijst-werkbonnen"] = f => OffertelijstAsync(f, VerkoopFase.LopendeWerkbonnen),
    };

    public static async Task AllesAsync(string uitvoer, HashSet<string> gekozen)
    {
        foreach (var (naam, maak) in Scenarios.Where(s => gekozen.Count == 0 || gekozen.Contains(s.Key)))
        {
            await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
            await TestGegevens.SeedAsync(scope.Factory);
            var scherm = await maak(scope.Factory);
            var pad = Path.Combine(uitvoer, naam + ".png");
            await RenderAsync(scherm, pad);
            Console.WriteLine(pad);
        }
    }

    /// <summary>Toont het scherm in een venster van 1440×900 (zoals op de Mac) en bewaart een PNG.</summary>
    public static async Task RenderAsync(Control scherm, string pad, int breedte = 1440, int hoogte = 900)
    {
        var venster = new Window { Width = breedte, Height = hoogte, Content = scherm };
        venster.Show();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        venster.CaptureRenderedFrame()!.Save(pad);
        venster.Close();
    }

    static async Task<Control> OfferteAsync(IDbContextFactory<AppDbContext> f, int offerteNummer)
    {
        var toast = new TestToastService();
        var auth = new TestAuthService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var pricing = new PricingService(f, new PricingSettingsProvider(f), new PricingEngine(), NullLogger<PricingService>.Instance);
        var factuurWf = new FactuurWorkflowService(f, pricing, auth);
        var vm = new OfferteViewModel(f, new TestNavigationService(), new TestDialogService(), pricing,
            new OfferteWorkflowService(f, wf), new WerkBonWorkflowService(f, wf), wf, factuurWf,
            new FactuurExportService(f, new IFactuurExporter[] { new PdfFactuurExporter() }), new TestFilePickerService(),
            new OfferteValidator(f), toast, new KlantValidator(f), new KlantDialogService(),
            new OntvangstService(f, auth, factuurWf), new TestPathOpener(), new GeenOfferteNavigatie(), auth);

        await using var db = await f.CreateDbContextAsync();
        var id = await db.Offertes.Where(o => o.OfferteNummer == offerteNummer).Select(o => o.Id).SingleAsync();
        await vm.InitializeAsync();
        await vm.LoadAsync(id);
        await Task.Delay(300);
        return new OfferteView { DataContext = vm };
    }

    static async Task<Control> OffertelijstAsync(IDbContextFactory<AppDbContext> f, VerkoopFase tabblad)
    {
        var vm = new OffertesLijstViewModel(f, new GeenOfferteNavigatie(), new TestDialogService(),
            new TestNavigationService(), new TestToastService(),
            new OfferteArchiefService(f, new TestAuthService(), NullLogger<OfferteArchiefService>.Instance), new VerkoopOverzichtService(f));
        await vm.LoadAsync();
        vm.GeselecteerdeFase = tabblad;
        return new OffertesLijstView { DataContext = vm };
    }

    sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }
}

/// <summary>Vaste testgegevens: offerte 419 (concept), 418 (verzonden), 412 (bevestigd, werkbon gepland),
/// 398 (afgehaald en betaald).</summary>
static class TestGegevens
{
    public static async Task SeedAsync(IDbContextFactory<AppDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();
        foreach (var (c, n) in new[] { ('G', "Glas"), ('P', "Passe-partout"), ('D', "Diepte"), ('O', "Opkleven"), ('R', "Rug") })
            db.AfwerkingsGroepen.Add(new AfwerkingsGroep { Code = c, Naam = n });

        var klant = new Klant { Voornaam = "Test", Achternaam = "Quadro", Gemeente = "Aarschot", Email = "test@quadro.test" };
        var kader = new KantKlaarKader { Naam = "C2", Beschrijving = "Alu zwart 50×70", PrijsPerStukExcl = 37.19m };

        OfferteRegel Regel() => new()
        {
            AantalStuks = 1, BreedteCm = 50, HoogteCm = 70, Titel = "Trouwfoto", KantKlaarKader = kader,
            TotaalExcl = 37.19m, SubtotaalExBtw = 37.19m, BtwBedrag = 7.81m, TotaalInclBtw = 45m
        };
        Offerte Offerte(int nr, OfferteStatus status) => new()
        {
            OfferteNummer = nr, Datum = new DateTime(2026, 10, 19), Klant = klant, Status = status,
            SubtotaalExBtw = 37.19m, BtwBedrag = 7.81m, TotaalInclBtw = 45m, Regels = { Regel() }
        };

        var concept = Offerte(419, OfferteStatus.Concept);
        var verzonden = Offerte(418, OfferteStatus.Verzonden);
        var bevestigd = Offerte(412, OfferteStatus.Goedgekeurd);
        bevestigd.WerkBon = new WerkBon { Status = WerkBonStatus.Gepland, TotaalPrijsIncl = 45m };
        var afgerond = Offerte(398, OfferteStatus.Betaald);
        afgerond.WerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald, TotaalPrijsIncl = 45m };
        db.Offertes.AddRange(concept, verzonden, bevestigd, afgerond);
        await db.SaveChangesAsync();

        db.Facturen.Add(new Factuur
        {
            OfferteId = afgerond.Id, WerkBonId = afgerond.WerkBon!.Id, Jaar = 2026, VolgNr = 19, FactuurNummer = "2026-19",
            KlantNaam = "Test Quadro", TotaalInclBtw = 45m, Status = FactuurStatus.Betaald
        });
        await db.SaveChangesAsync();
    }
}

sealed class RenderApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://QuadroApp/"))
        {
            Source = new Uri("avares://QuadroApp/Styles/QuadroTheme.axaml")
        });
    }
}
