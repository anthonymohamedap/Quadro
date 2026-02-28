using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Toast;
using QuadroApp.Services;
using QuadroApp.Services.Import;
using QuadroApp.Services.Pricing;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace QuadroApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ==============================
        // 1️⃣ GLOBAL EXCEPTION HANDLING
        // ==============================

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            LogException(e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogException(e.Exception);
            e.SetObserved();
        };

        // ==============================
        // 2️⃣ DEPENDENCY INJECTION
        // ==============================

        var services = new ServiceCollection();

        // 🔹 Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 🔹 Database
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlite("Data Source=quadro.db");
        });

        var dbPath = Path.GetFullPath("quadro.db");
        Console.WriteLine($"[DB] SQLite path = {dbPath}");

        // ==============================
        // 3️⃣ NAVIGATION & UI SERVICES
        // ==============================

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IOfferteNavigationService, OfferteNavigationService>();

        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IToastService, ToastService>();

        services.AddTransient<IKlantDialogService, KlantDialogService>();
        services.AddTransient<ILijstDialogService, LijstDialogService>();

        // ==============================
        // 4️⃣ DOMAIN SERVICES
        // ==============================

        services.AddScoped<IAfwerkingenService, AfwerkingenService>();
        services.AddScoped<IWorkflowService, QuadroApp.Service.WorkflowService>();
        services.AddScoped<IOfferteWorkflowService, OfferteWorkflowService>();
        services.AddScoped<IWerkBonWorkflowService, WerkBonWorkflowService>();

        services.AddSingleton<IPricingService, PricingService>();

        // Import
        services.AddTransient<IExcelImportService, ExcelImportService>();
        services.AddTransient<KlantExcelImportService>();
        services.AddTransient<AfwerkingsOptieExcelImportService>();

        // Validators
        services.AddScoped<ICrudValidator<TypeLijst>, TypeLijstValidator>();
        services.AddScoped<ICrudValidator<Klant>, KlantValidator>();
        services.AddTransient<ICrudValidator<AfwerkingsOptie>, AfwerkingsOptieValidator>();
        services.AddScoped<IOfferteValidator, OfferteValidator>();

        // ==============================
        // 5️⃣ VIEWMODELS
        // ==============================

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<KlantenViewModel>();
        services.AddTransient<LijstenViewModel>();
        services.AddTransient<AfwerkingenViewModel>();
        services.AddTransient<OffertesLijstViewModel>();
        services.AddTransient<OfferteViewModel>();
        services.AddTransient<KlantDetailViewModel>();
        services.AddTransient<WerkBonLijstViewModel>();

        Services = services.BuildServiceProvider();

        // ==============================
        // 6️⃣ DATABASE INITIALIZATION
        // ==============================

        try
        {
            InitializeDatabaseAsync(Services).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogException(ex);
            throw;
        }

        // ==============================
        // 7️⃣ MAIN WINDOW
        // ==============================

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    private static void LogException(Exception? ex)
    {
        if (ex == null) return;

        var text = $"[{DateTime.Now}] {ex}\n\n";
        File.AppendAllText("crash.log", text);

        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = new Window
            {
                Width = 500,
                Height = 200,
                Content = new TextBlock
                {
                    Text = "Er is een fout opgetreden.\nZie crash.log",
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };

            win.ShowDialog(desktop.MainWindow);
        }
    }
    private static async Task InitializeDatabaseAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        Console.WriteLine("[DB] Resetting demo database...");

        // 🔥 Demo reset
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // Seed data
        SeedBasisData(db);

        Console.WriteLine("[DB] Klanten rows = " + await db.Klanten.CountAsync());
        Console.WriteLine("[DB] TypeLijsten rows = " + await db.TypeLijsten.CountAsync());
        Console.WriteLine("[DB] Offertes rows = " + await db.Offertes.CountAsync());
    }


    public class ToastColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                ToastType.Success => new SolidColorBrush(Color.Parse("#52c41a")),
                ToastType.Error => new SolidColorBrush(Color.Parse("#ff4d4f")),
                ToastType.Warning => new SolidColorBrush(Color.Parse("#faad14")),
                _ => new SolidColorBrush(Color.Parse("#1677ff")),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private static void SeedData(AppDbContext db)
    {
        if (!db.Leveranciers.Any())
        {
            db.Leveranciers.AddRange(
                new Leverancier { Code = "ICO", Naam = "Iconic Frames" },
                new Leverancier { Code = "HOF", Naam = "Hofman Design" },
                new Leverancier { Code = "FRA", Naam = "FramingArt" },
                new Leverancier { Code = "BOL", Naam = "Bol FrameWorks" }
            );
            db.SaveChanges();
        }

        // IDs ophalen op basis van Code
        int icoId = db.Leveranciers.Single(l => l.Code == "ICO").Id;
        int hofId = db.Leveranciers.Single(l => l.Code == "HOF").Id;
        int fraId = db.Leveranciers.Single(l => l.Code == "FRA").Id;
        int bolId = db.Leveranciers.Single(l => l.Code == "BOL").Id;

        // 2) AfwerkingsGroepen (G / P / D / O / R)
        if (!db.AfwerkingsGroepen.Any())
        {
            db.AfwerkingsGroepen.AddRange(
                new AfwerkingsGroep { Code = 'G', Naam = "Glas" },
                new AfwerkingsGroep { Code = 'P', Naam = "Passe-partout" },
                new AfwerkingsGroep { Code = 'D', Naam = "Diepte kern" },
                new AfwerkingsGroep { Code = 'O', Naam = "Opkleven" },
                new AfwerkingsGroep { Code = 'R', Naam = "Rug" }
            );
            db.SaveChanges();
        }

        int gId = db.AfwerkingsGroepen.Single(g => g.Code == 'G').Id;
        int pId = db.AfwerkingsGroepen.Single(g => g.Code == 'P').Id;
        int dId = db.AfwerkingsGroepen.Single(g => g.Code == 'D').Id;
        int oId = db.AfwerkingsGroepen.Single(g => g.Code == 'O').Id;
        int rId = db.AfwerkingsGroepen.Single(g => g.Code == 'R').Id;

        // 3) TypeLijsten (jouw 10 records)
        if (!db.TypeLijsten.Any())
        {
            db.TypeLijsten.AddRange(
                new TypeLijst
                {
                    Artikelnummer = "001001/A1",
                    LeverancierId = icoId,
                    BreedteCm = 20,
                    Soort = "HOUT",
                    Serie = "Classic",
                    IsDealer = true,
                    Opmerking = "Zwarte houten lijst 20mm plat profiel",
                    PrijsPerMeter = 6.50m,
                    WinstMargeFactor = 2.200m,
                    AfvalPercentage = 12.5m,
                    VasteKost = 1.50m,
                    WerkMinuten = 8,
                    VoorraadMeter = 120.00m,
                    InventarisKost = 780.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 15.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001002/A2",
                    LeverancierId = icoId,
                    BreedteCm = 25,
                    Soort = "HOUT",
                    Serie = "Modern",
                    IsDealer = true,
                    Opmerking = "Witte lijst met glans afwerking",
                    PrijsPerMeter = 7.10m,
                    WinstMargeFactor = 2.300m,
                    AfvalPercentage = 10.0m,
                    VasteKost = 1.80m,
                    WerkMinuten = 7,
                    VoorraadMeter = 100.00m,
                    InventarisKost = 710.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 10.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001003/B1",
                    LeverancierId = hofId,
                    BreedteCm = 22,
                    Soort = "HOUT",
                    Serie = "Classic",
                    IsDealer = false,
                    Opmerking = "Donkere eiken lijst met structuur",
                    PrijsPerMeter = 8.90m,
                    WinstMargeFactor = 2.500m,
                    AfvalPercentage = 15.0m,
                    VasteKost = 2.20m,
                    WerkMinuten = 10,
                    VoorraadMeter = 75.00m,
                    InventarisKost = 667.50m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 8.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001004/B2",
                    LeverancierId = hofId,
                    BreedteCm = 18,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = false,
                    Opmerking = "Aluminium profiel zwart mat",
                    PrijsPerMeter = 4.95m,
                    WinstMargeFactor = 2.000m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 1.20m,
                    WerkMinuten = 6,
                    VoorraadMeter = 200.00m,
                    InventarisKost = 990.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 20.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001005/C1",
                    LeverancierId = fraId,
                    BreedteCm = 28,
                    Soort = "PVC",
                    Serie = "Budget",
                    IsDealer = true,
                    Opmerking = "Wit profiel met fijne ribbels",
                    PrijsPerMeter = 3.80m,
                    WinstMargeFactor = 2.100m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.90m,
                    WerkMinuten = 7,
                    VoorraadMeter = 300.00m,
                    InventarisKost = 1140.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 25.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001006/C2",
                    LeverancierId = fraId,
                    BreedteCm = 30,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = true,
                    Opmerking = "Zilver profiel 30mm breed",
                    PrijsPerMeter = 5.40m,
                    WinstMargeFactor = 2.150m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 1.30m,
                    WerkMinuten = 5,
                    VoorraadMeter = 180.00m,
                    InventarisKost = 972.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 15.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001007/D1",
                    LeverancierId = bolId,
                    BreedteCm = 35,
                    Soort = "HOUT",
                    Serie = "Lux",
                    IsDealer = false,
                    Opmerking = "Hoge lijst donker eik afgerond",
                    PrijsPerMeter = 8.90m,
                    WinstMargeFactor = 2.500m,
                    AfvalPercentage = 12.0m,
                    VasteKost = 2.50m,
                    WerkMinuten = 10,
                    VoorraadMeter = 75.00m,
                    InventarisKost = 667.50m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 10.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001008/D2",
                    LeverancierId = bolId,
                    BreedteCm = 15,
                    Soort = "PVC",
                    Serie = "Budget",
                    IsDealer = false,
                    Opmerking = "Smalle witte kunststof lijst",
                    PrijsPerMeter = 3.20m,
                    WinstMargeFactor = 2.050m,
                    AfvalPercentage = 7.0m,
                    VasteKost = 0.80m,
                    WerkMinuten = 6,
                    VoorraadMeter = 250.00m,
                    InventarisKost = 800.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 30.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001009/E1",
                    LeverancierId = icoId,
                    BreedteCm = 40,
                    Soort = "HOUT",
                    Serie = "Lux",
                    IsDealer = true,
                    Opmerking = "Brede lijst mahonie met nerf",
                    PrijsPerMeter = 9.80m,
                    WinstMargeFactor = 2.600m,
                    AfvalPercentage = 14.0m,
                    VasteKost = 2.80m,
                    WerkMinuten = 12,
                    VoorraadMeter = 60.00m,
                    InventarisKost = 588.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 8.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001010/E2",
                    LeverancierId = hofId,
                    BreedteCm = 12,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = false,
                    Opmerking = "Aluminium profiel zilver glans",
                    PrijsPerMeter = 4.50m,
                    WinstMargeFactor = 2.000m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 1.00m,
                    WerkMinuten = 5,
                    VoorraadMeter = 210.00m,
                    InventarisKost = 945.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 25.00m
                }
            );

            db.SaveChanges();
        }

        // 4) AfwerkingsOpties (G, P, D, O, R – alle rijen die je stuurde)
        if (!db.AfwerkingsOpties.Any())
        {
            db.AfwerkingsOpties.AddRange(
                // 🪟 GLAS (G) – HOF
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Helder glas 2 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 18.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = hofId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Ontspiegeld glas 2 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 26.50m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = hofId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Museumglas UV-filter",
                    Volgnummer = '3',
                    KostprijsPerM2 = 60.00m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 4.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 4,
                    LeverancierId = hofId
                },

                // 🎨 PASSE-PARTOUT (P) – FRA
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Passe-partout wit",
                    Volgnummer = '1',
                    KostprijsPerM2 = 12.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Passe-partout zwart",
                    Volgnummer = '2',
                    KostprijsPerM2 = 13.50m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Dubbele passe-partout",
                    Volgnummer = '3',
                    KostprijsPerM2 = 18.00m,
                    WinstMarge = 2.20m,
                    AfvalPercentage = 10.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 7,
                    LeverancierId = fraId
                },

                // 🧱 DIEPTE / KERN (D) – BOL
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "Foamboard 5 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 15.50m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = bolId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "Foamboard 10 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 19.00m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = bolId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "PVC-kern 5 mm",
                    Volgnummer = '3',
                    KostprijsPerM2 = 22.00m,
                    WinstMarge = 2.20m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 4,
                    LeverancierId = bolId
                },

                // 🧷 OPKLEVEN (O) – ICO
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven zuurvrij",
                    Volgnummer = '1',
                    KostprijsPerM2 = 10.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 6,
                    LeverancierId = icoId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven spray-mount",
                    Volgnummer = '2',
                    KostprijsPerM2 = 8.50m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = icoId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven film",
                    Volgnummer = '3',
                    KostprijsPerM2 = 11.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 6,
                    LeverancierId = icoId
                },

                // 🪵 RUG (R) – FRA
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "MDF 3 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 9.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 2,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "Hardboard 3 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 8.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 2,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "Zuurvrij karton",
                    Volgnummer = '3',
                    KostprijsPerM2 = 11.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = fraId
                }
            );

            db.SaveChanges();
        }

        // 5) Basis instellingen (optioneel, maar handig voor PricingService)
        if (!db.Instellingen.Any(i => i.Sleutel == "Uurloon"))
        {
            db.Instellingen.Add(new Instelling
            {
                Sleutel = "Uurloon",
                Waarde = "45"
            });
        }

        if (!db.Instellingen.Any(i => i.Sleutel == "BtwPercent"))
        {
            db.Instellingen.Add(new Instelling
            {
                Sleutel = "BtwPercent",
                Waarde = "21"
            });
        }
        // 6) Klanten
        // 6) Klanten
        if (!db.Klanten.Any())
        {
            db.Klanten.AddRange(
                new Klant
                {
                    Voornaam = "Jan",
                    Achternaam = "Peeters",
                    Email = "jan.peeters@email.be",
                    Telefoon = "0471 12 34 56",
                    Straat = "Kerkstraat",
                    Nummer = "12",
                    Postcode = "2000",
                    Gemeente = "Antwerpen",
                    Opmerking = "Particuliere klant"
                },
                new Klant
                {
                    Voornaam = "Sofie",
                    Achternaam = "Van den Broeck",
                    Email = "sofie.vdb@email.be",
                    Telefoon = "0485 45 67 89",
                    Straat = "Meir",
                    Nummer = "85",
                    Postcode = "2000",
                    Gemeente = "Antwerpen",
                    Opmerking = "Regelmatige klant"
                },
                new Klant
                {
                    Voornaam = "Thomas",
                    Achternaam = "De Smet",
                    Email = "info@desmetdesign.be",
                    Telefoon = "03 123 45 67",
                    Straat = "Industrieweg",
                    Nummer = "7",
                    Postcode = "2800",
                    Gemeente = "Mechelen",
                    BtwNummer = "BE0123456789",
                    Opmerking = "Zakelijke klant"
                },
                new Klant
                {
                    Voornaam = "Emma",
                    Achternaam = "Janssens",
                    Email = "emma.j@email.be",
                    Telefoon = "0499 88 77 66",
                    Straat = "Stationsstraat",
                    Nummer = "44",
                    Postcode = "9000",
                    Gemeente = "Gent",
                    Opmerking = "Nieuwe klant"
                }
            );

            db.SaveChanges();
        }
        if (db.ChangeTracker.HasChanges())
        {
            db.SaveChanges();
        }

    }
    private static void SeedBasisData(AppDbContext db)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        SeedData(db);
    }


}
