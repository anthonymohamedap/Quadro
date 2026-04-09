using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Toast;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
namespace QuadroApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private static ILogger<App> _logger = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ==============================
        // 1️⃣ GLOBAL EXCEPTION HANDLING
        // ==============================

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogException(e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
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

        // ==============================
        // 3️⃣ NAVIGATION & UI SERVICES
        // ==============================

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IOfferteNavigationService, OfferteNavigationService>();

        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IPathOpener, PathOpener>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddTransient<IKlantDialogService, KlantDialogService>();
        services.AddTransient<ILijstDialogService, LijstDialogService>();

        // ==============================
        // 4️⃣ DOMAIN SERVICES
        // ==============================

        services.AddScoped<IAfwerkingenService, AfwerkingenService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IWerkBonArchiefService, WerkBonArchiefService>();
        services.AddScoped<IOfferteArchiefService, OfferteArchiefService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IOfferteWorkflowService, OfferteWorkflowService>();
        services.AddScoped<IWerkBonWorkflowService, WerkBonWorkflowService>();
        services.AddScoped<IFactuurWorkflowService, FactuurWorkflowService>();
        services.AddScoped<IFactuurExportService, FactuurExportService>();
        services.AddScoped<ICentralExcelExportService, CentralExcelExportService>();
        services.AddScoped<IFactuurExporter, PdfFactuurExporter>();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<PricingEngine>();
        services.AddSingleton<IPricingSettingsProvider, PricingSettingsProvider>();
        services.AddSingleton<IPricingService, PricingService>();
        services.AddSingleton<IAppSettingsProvider, AppSettingsProvider>();

        // Enterprise import pipeline
        services.AddTransient<IExcelParser, ClosedXmlExcelParser>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<IExcelMap<Klant>, KlantExcelMap>();
        services.AddTransient<IImportValidator<Klant>, KlantImportValidator>();
        services.AddTransient<IImportCommitter<Klant>, KlantImportCommitter>();
        services.AddTransient<KlantImportDefinition>();

        services.AddTransient<IExcelMap<TypeLijst>, TypeLijstExcelMap>();
        services.AddTransient<IImportValidator<TypeLijst>, TypeLijstImportValidator>();
        services.AddTransient<IImportCommitter<TypeLijst>, TypeLijstImportCommitter>();
        services.AddTransient<TypeLijstImportDefinition>();

        services.AddTransient<IExcelMap<AfwerkingsOptie>, AfwerkingsOptieExcelMap>();
        services.AddTransient<IImportValidator<AfwerkingsOptie>, AfwerkingsOptieImportValidator>();
        services.AddTransient<IImportCommitter<AfwerkingsOptie>, AfwerkingsOptieImportCommitter>();
        services.AddTransient<AfwerkingsOptieImportDefinition>();

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
        services.AddTransient<LeveranciersViewModel>();
        services.AddTransient<AfwerkingenViewModel>();
        services.AddTransient<OffertesLijstViewModel>();
        services.AddTransient<OfferteViewModel>();
        services.AddTransient<KlantDetailViewModel>();
        services.AddTransient<WerkBonLijstViewModel>();
        services.AddTransient<ArchiefViewModel>();
        services.AddTransient<FacturenViewModel>();
        services.AddTransient<ExportCenterViewModel>();
        services.AddTransient<InstellingenViewModel>();

        Services = services.BuildServiceProvider();
        _logger = Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("[DB] SQLite path = {DbPath}", dbPath);

        // ==============================
        // 6️⃣ DATABASE INITIALIZATION
        // ==============================

        try
        {
            InitializeDatabaseAsync(Services).GetAwaiter().GetResult();
            RunStartupTasksAsync(Services).GetAwaiter().GetResult();
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
            Dispatcher.UIThread.Post(() =>
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

                var owner = desktop.MainWindow;
                if (owner is null)
                {
                    win.Show();
                    return;
                }

                _ = win.ShowDialog(owner);
            });
        }
    }
    private static async Task InitializeDatabaseAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        // Ensure DB exists (for new installs)
        await db.Database.EnsureCreatedAsync();

        // Apply pending migrations safely.
        // For DBs created via EnsureCreatedAsync (no __EFMigrationsHistory),
        // first mark all previously-existing migrations as applied so we don't re-run them.
        await ApplyPendingMigrationsAsync(db);

        // Seed data (single source of truth)
        DbSeeder.SeedDemoData(db);

        _logger.LogInformation("[DB] Klanten={K}, TypeLijsten={L}, Offertes={O}",
            await db.Klanten.CountAsync(),
            await db.TypeLijsten.CountAsync(),
            await db.Offertes.CountAsync());
    }

    /// <summary>
    /// Applies EF Core migrations safely, even on a DB that was originally
    /// created via EnsureCreatedAsync (which has no __EFMigrationsHistory table).
    /// Marks all pre-existing migrations as applied, then runs only new ones.
    /// </summary>
    private static async Task ApplyPendingMigrationsAsync(AppDbContext db)
    {
        const string historyTable = "__EFMigrationsHistory";

        // ── Stap 1: archief-tabellen ALTIJD aanmaken via raw SQL ─────────────
        // Dit staat los van het migratie-systeem zodat een vroeg-falende catch
        // de tabelcreatie niet kan overslaan.
#pragma warning disable EF1002  // Raw SQL with no user input — safe
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"WerkBonArchieven\" (" +
                "\"Id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"OrigineleWerkBonId\" INTEGER NOT NULL," +
                "\"OfferteId\" INTEGER NOT NULL," +
                "\"KlantNaam\" TEXT NOT NULL," +
                "\"KlantId\" INTEGER NULL," +
                "\"OfferteDatum\" TEXT NOT NULL," +
                "\"OfferteStatusOpMoment\" TEXT NOT NULL," +
                "\"WerkBonStatusOpMoment\" TEXT NOT NULL," +
                "\"TotaalPrijsIncl\" TEXT NOT NULL," +
                "\"GearchiveerdOp\" TEXT NOT NULL," +
                "\"AnnuleringsReden\" TEXT NULL," +
                "\"Snapshot\" TEXT NOT NULL," +
                "\"IsHersteld\" INTEGER NOT NULL," +
                "\"HersteldNaarOfferteId\" INTEGER NULL" +
                ")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_GearchiveerdOp""     ON ""WerkBonArchieven""(""GearchiveerdOp"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OrigineleWerkBonId"" ON ""WerkBonArchieven""(""OrigineleWerkBonId"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_WerkBonArchieven_OfferteId""          ON ""WerkBonArchieven""(""OfferteId"")");

            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"OfferteArchieven\" (" +
                "\"Id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"OrigineleOfferteId\" INTEGER NOT NULL," +
                "\"KlantNaam\" TEXT NOT NULL," +
                "\"KlantId\" INTEGER NULL," +
                "\"OfferteDatum\" TEXT NOT NULL," +
                "\"Jaar\" INTEGER NOT NULL," +
                "\"StatusOpMoment\" TEXT NOT NULL," +
                "\"TotaalInclBtw\" TEXT NOT NULL," +
                "\"HadWerkBon\" INTEGER NOT NULL," +
                "\"GearchiveerdOp\" TEXT NOT NULL," +
                "\"Reden\" TEXT NULL," +
                "\"Snapshot\" TEXT NOT NULL," +
                "\"IsHersteld\" INTEGER NOT NULL," +
                "\"HersteldNaarOfferteId\" INTEGER NULL" +
                ")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_GearchiveerdOp""     ON ""OfferteArchieven""(""GearchiveerdOp"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_Jaar""               ON ""OfferteArchieven""(""Jaar"")");
            await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_OfferteArchieven_OrigineleOfferteId"" ON ""OfferteArchieven""(""OrigineleOfferteId"")");

            _logger.LogInformation("[DB] Archief-tabellen gecontroleerd/aangemaakt.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB] FOUT bij aanmaken archief-tabellen: {Message}", ex.Message);
        }

        // ── Stap 2: EF migratie-historietabel + pre-existing migrations ──────
        var preExistingMigrations = new[]
        {
            "20260228232856_InitialClean",
            "20260303090000_AddStaaflijstSettingsAndFlag",
            "20260303091000_RemoveTypeLijstMarginColumns",
            "20260303141137_fixes",
            "20260318000000_AddTypeLijstPerLijstPricingDropStaaflijst",
            "20260321121423_AddTitelToOfferteRegel",
            "20260323120500_AddAfwerkingsKleur",
            "20260407000000_NullableLeverancierIdOnTypeLijst",
            // "20260408161449_home" — removed: no matching .cs migration file exists,
            // would crash MigrateAsync on an existing DB that doesn't have it applied.
            "20260408120000_AddWerkBonArchief",
            "20260408140000_AddOfferteArchief",
        };

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE TABLE IF NOT EXISTS \"{historyTable}\" " +
                "(\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL)");

            foreach (var m in preExistingMigrations)
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"INSERT OR IGNORE INTO \"{historyTable}\" VALUES ('{m}', '9.0.0')");
            }

            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Migration] Warning: {Message}", ex.Message);
        }
#pragma warning restore EF1002
    }

    private static async Task RunStartupTasksAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();

        _logger.LogInformation("[Startup] Refreshing voorraad alerts...");
        await stockService.RefreshAlertsAsync();
    }


    public class ToastColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                ToastType.Success => new SolidColorBrush(Color.Parse("#52c41a")),
                ToastType.Error => new SolidColorBrush(Color.Parse("#ff4d4f")),
                ToastType.Warning => new SolidColorBrush(Color.Parse("#faad14")),
                _ => new SolidColorBrush(Color.Parse("#1677ff")),
            };
        }

        public object? ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }




}

