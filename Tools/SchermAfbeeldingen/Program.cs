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
        ["bestelbonnen"] = BestelbonnenAsync,
        ["home"] = f => HomeAsync(f, metVandaag: true),
        ["home-leeg"] = f => HomeAsync(f, metVandaag: false),
        ["home-vol"] = async f => { await TestGegevens.SeedVeelAsync(f, 10); return await HomeAsync(f, metVandaag: false); },
        ["home-druk"] = async f => { await TestGegevens.SeedWaarschuwingenAsync(f); return await HomeAsync(f, metVandaag: true); },
        ["home-druk-open"] = async f => { await TestGegevens.SeedWaarschuwingenAsync(f); return await HomeAsync(f, metVandaag: true, voorraadOpen: true); },
        ["home-klein"] = async f => { await TestGegevens.SeedWaarschuwingenAsync(f); await TestGegevens.SeedVeelAsync(f, 10); return await HomeAsync(f, metVandaag: false); },
    };

    /// <summary>Scenario's met een ander venster dan 1440×900.</summary>
    static readonly Dictionary<string, (int Breedte, int Hoogte)> Formaten = new()
    {
        ["home-klein"] = (1024, 768),
    };

    public static async Task AllesAsync(string uitvoer, HashSet<string> gekozen)
    {
        foreach (var (naam, maak) in Scenarios.Where(s => gekozen.Count == 0 || gekozen.Contains(s.Key)))
        {
            await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
            await TestGegevens.SeedAsync(scope.Factory);
            var scherm = await maak(scope.Factory);
            var pad = Path.Combine(uitvoer, naam + ".png");
            var (b, h) = Formaten.GetValueOrDefault(naam, (1440, 900));
            await RenderAsync(scherm, pad, b, h);
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
        var bestelbonWf = new BestelbonWorkflowService(f, pricing, auth);
        var vm = new OfferteViewModel(f, new TestNavigationService(), new TestDialogService(), pricing,
            new OfferteWorkflowService(f, wf), new WerkBonWorkflowService(f, wf), wf, bestelbonWf,
            new BestelbonExportService(f, new IBestelbonExporter[] { new PdfBestelbonExporter() }), new TestFilePickerService(),
            new OfferteValidator(f), toast, new KlantValidator(f), new KlantDialogService(),
            new OntvangstService(f, auth, bestelbonWf), new TestPathOpener(), new GeenOfferteNavigatie(), auth);

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

    static async Task<Control> BestelbonnenAsync(IDbContextFactory<AppDbContext> f)
    {
        await TestGegevens.SeedVandaagAsync(f);
        var toast = new TestToastService();
        var auth = new TestAuthService();
        var pricing = new PricingService(f, new PricingSettingsProvider(f), new PricingEngine(), NullLogger<PricingService>.Instance);
        var workflow = new BestelbonWorkflowService(f, pricing, auth);
        var vm = new BestelbonnenViewModel(f, workflow,
            new BestelbonExportService(f, new IBestelbonExporter[] { new PdfBestelbonExporter() }), toast,
            new TestNavigationService(), new OntvangstService(f, auth, workflow), new TestPathOpener());
        await vm.InitializeAsync();
        vm.GeselecteerdeBestelbon = vm.Bestelbonnen.FirstOrDefault(b => b.BestelbonNummer == "2026-23");
        await Task.Delay(300);
        return new BestelbonnenView { DataContext = vm };
    }

    static async Task<Control> HomeAsync(IDbContextFactory<AppDbContext> f, bool metVandaag, bool voorraadOpen = false)
    {
        if (metVandaag) await TestGegevens.SeedVandaagAsync(f);
        var toast = new TestToastService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var vm = new HomeViewModel(new TestNavigationService(), f, new WerkBonWorkflowService(f, wf), toast, wf,
            new GeenServices(), new VerkoopOverzichtService(f), new GeenOfferteNavigatie());
        await vm.InitializeAsync();
        vm.IsVoorraadOpen = voorraadOpen;
        return new HomeView { DataContext = vm };
    }

    sealed class GeenServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }
}

/// <summary>Vaste testgegevens: offerte 419 (concept), 418 (verzonden), 412 (bevestigd, werkbon gepland),
/// 398 (afgehaald en betaald).</summary>
static partial class TestGegevens
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

        db.Bestelbonnen.Add(new Bestelbon
        {
            OfferteId = afgerond.Id, WerkBonId = afgerond.WerkBon!.Id, Jaar = 2026, VolgNr = 19, BestelbonNummer = "2026-19",
            KlantNaam = "Test Quadro", TotaalInclBtw = 45m, Status = BestelbonStatus.Betaald
        });
        await db.SaveChangesAsync();
    }
}

/// <summary>Extra voor het beginscherm (US-86): verkopen met afhaaldatum vandaag en afgehaalde
/// bonnen met een rest.</summary>
static partial class TestGegevens
{
    /// <summary>Open voorraadwaarschuwingen zoals VoorraadAlertService ze maakt (lijsten en leveringen).</summary>
    public static async Task SeedWaarschuwingenAsync(IDbContextFactory<AppDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();
        var nu = DateTime.UtcNow;
        VoorraadAlert A(VoorraadAlertType t, string bericht, int minutenGeleden) =>
            new() { AlertType = t, Bericht = bericht, AangemaaktOp = nu.AddMinutes(-minutenGeleden) };
        db.VoorraadAlerts.AddRange(
            A(VoorraadAlertType.BelowMinimum, "Voorraad onder minimum voor L-2045 (1,8 m beschikbaar).", 5),
            A(VoorraadAlertType.LowStock, "Voorraad bijna op voor L-1180 (4,5 m beschikbaar).", 10),
            A(VoorraadAlertType.LowStock, "Voorraad bijna op voor ALU-50Z (6 m beschikbaar).", 15),
            A(VoorraadAlertType.OpenShortage, "Open tekort voor werktaak 87 (L-2045).", 20),
            A(VoorraadAlertType.OrderOverdue, "Bestelling B-2026-031 voor leverancier Nielsen is over tijd.", 25),
            A(VoorraadAlertType.PartialReceiptPending, "Bestelling B-2026-029 is deels ontvangen en nog niet afgerond.", 30),
            A(VoorraadAlertType.OrderOverdue, "Bestelling B-2026-027 voor leverancier Larson-Juhl is over tijd.", 35));
        await db.SaveChangesAsync();
    }

    /// <summary>Drukke dag: <paramref name="aantal"/> verkopen vandaag af te halen en evenveel afgehaald met een rest.</summary>
    public static async Task SeedVeelAsync(IDbContextFactory<AppDbContext> f, int aantal)
    {
        await using var db = await f.CreateDbContextAsync();
        var vandaag = DateTime.Today;
        string[] voornamen = { "Marleen", "Luc", "Sofie", "Jan", "Els", "Tom", "An", "Pieter", "Lien", "Koen" };
        string[] namen = { "Peeters", "Janssens", "Claes", "Wouters", "Maes", "Jacobs", "Mertens", "Willems", "Goossens", "Aerts" };
        var lijst = new List<Offerte>();
        for (var i = 0; i < aantal; i++)
        {
            var totaal = 60m + i * 23.5m;
            lijst.Add(new Offerte
            {
                OfferteNummer = 430 + i, Datum = vandaag.AddDays(-14), Status = OfferteStatus.Afgewerkt,
                Klant = new Klant { Voornaam = voornamen[i % 10], Achternaam = namen[i % 10] },
                TotaalInclBtw = totaal, VoorschotBedrag = i % 3 == 0 ? 0m : 40m, AfhaalDatum = vandaag,
                WerkBon = new WerkBon { Status = WerkBonStatus.Afgewerkt, TotaalPrijsIncl = totaal, AfhaalDatum = vandaag }
            });
            lijst.Add(new Offerte
            {
                OfferteNummer = 380 + i, Datum = vandaag.AddDays(-40), Status = OfferteStatus.Besteld,
                Klant = new Klant { Voornaam = voornamen[(i + 3) % 10], Achternaam = namen[(i + 5) % 10] },
                TotaalInclBtw = totaal + 40m, VoorschotBedrag = 40m, AfhaalDatum = vandaag.AddDays(-2 - i),
                WerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald, TotaalPrijsIncl = totaal + 40m, AfhaalDatum = vandaag.AddDays(-2 - i) }
            });
        }
        db.Offertes.AddRange(lijst);
        await db.SaveChangesAsync();
    }

    public static async Task SeedVandaagAsync(IDbContextFactory<AppDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();
        var vandaag = DateTime.Today;
        Klant K(string v, string a) => new() { Voornaam = v, Achternaam = a, Gemeente = "Aarschot" };
        Offerte O(int nr, Klant k, OfferteStatus st, decimal totaal, decimal voorschot, WerkBonStatus wb, DateTime? afhaal) => new()
        {
            OfferteNummer = nr, Datum = vandaag.AddDays(-14), Klant = k, Status = st,
            TotaalInclBtw = totaal, VoorschotBedrag = voorschot, AfhaalDatum = afhaal,
            WerkBon = new WerkBon { Status = wb, TotaalPrijsIncl = totaal, AfhaalDatum = afhaal }
        };

        // Vandaag af te halen: klaar (met voorschot), klaar zonder voorschot, en al afgehaald met rest.
        var peeters = O(421, K("Marleen", "Peeters"), OfferteStatus.Afgewerkt, 186.50m, 50m, WerkBonStatus.Afgewerkt, vandaag);
        var janssens = O(422, K("Luc", "Janssens"), OfferteStatus.Afgewerkt, 64.00m, 0m, WerkBonStatus.Afgewerkt, vandaag.AddHours(15));
        var claes = O(423, K("Sofie", "Claes"), OfferteStatus.Besteld, 245.00m, 100m, WerkBonStatus.Afgehaald, vandaag);
        // Nog te betalen: eerder afgehaald, rest staat open.
        var wouters = O(405, K("Jan", "Wouters"), OfferteStatus.Besteld, 120.00m, 40m, WerkBonStatus.Afgehaald, vandaag.AddDays(-6));
        // Niet op het beginscherm: klaar, maar pas morgen af te halen.
        var morgen = O(424, K("Els", "Maes"), OfferteStatus.Afgewerkt, 80m, 0m, WerkBonStatus.Afgewerkt, vandaag.AddDays(1));
        db.Offertes.AddRange(peeters, janssens, claes, wouters, morgen);
        await db.SaveChangesAsync();

        Bestelbon Bon(Offerte o, int volgNr, decimal voorschot) => new()
        {
            OfferteId = o.Id, WerkBonId = o.WerkBon!.Id, Jaar = 2026, VolgNr = volgNr, BestelbonNummer = $"2026-{volgNr}",
            KlantNaam = $"{o.Klant!.Voornaam} {o.Klant.Achternaam}", TotaalInclBtw = o.TotaalInclBtw,
            VoorschotBedrag = voorschot, Status = BestelbonStatus.KlaarVoorExport,
            BestelbonDatum = o.Datum, VervalDatum = o.Datum.AddDays(30),
            Lijnen =
            {
                new BestelbonLijn { Omschrijving = "Kader 50×70, alu zwart, ontspiegeld glas", Aantal = 1, BtwPct = 21,
                    TotaalIncl = o.TotaalInclBtw, TotaalExcl = Math.Round(o.TotaalInclBtw / 1.21m, 2),
                    PrijsExcl = Math.Round(o.TotaalInclBtw / 1.21m, 2), TotaalBtw = o.TotaalInclBtw - Math.Round(o.TotaalInclBtw / 1.21m, 2) }
            }
        };
        db.Bestelbonnen.AddRange(Bon(claes, 23, 100m), Bon(wouters, 21, 40m));
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
