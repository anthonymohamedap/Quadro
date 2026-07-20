using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Backup;
using QuadroApp.Service.Security;
using QuadroApp.Service.Toast;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QuadroApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private static ILogger<App> _logger = default!;

    /// <summary>
    /// Writable crash log path — resolved once at class load time so it is
    /// available before DI / ILogger are initialised.
    /// Windows : %LOCALAPPDATA%\QuadroApp\crash.log
    /// macOS   : ~/Library/Application Support/QuadroApp/crash.log
    /// </summary>
    private static readonly string _crashLogPath = BuildCrashLogPath();

    private static string BuildCrashLogPath()
    {
        var dir = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                           "Library", "Application Support", "QuadroApp")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                           "QuadroApp");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "crash.log");
    }

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

        // 🔹 Logging (US-31): Serilog roterend bestand + console/debug voor dev.
        var serilog = Service.LoggingSetup.CreateLogger(GetDataDirectory(), GetConfigValue("Logging:MinimumLevel"));
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
            builder.AddSerilog(serilog, dispose: true);
            builder.SetMinimumLevel(LogLevel.Debug); // Serilog filtert zelf op niveau
        });

        // 🔹 Database — connection string comes from appsettings.json when present,
        //   falls back to local SQLite so existing installs keep working without any config file.
        //   Provider is detected automatically from the connection string:
        //     "Host=..."   → PostgreSQL (shared LAN server, both PCs)
        //     "Data Source=..." → SQLite (local single-PC, default)
        var connectionString = GetConnectionString();
        var isPostgres = IsPostgresConnectionString(connectionString);

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            if (isPostgres)
                options.UseNpgsql(connectionString, npgsql =>
                {
                    // Retry up to 3 times on transient network errors (Wi-Fi blips, PG restart).
                    // Uses exponential backoff: 0s, 1s, 3s between attempts.
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
            else
                options.UseSqlite(connectionString);

            // US-30: schema wordt beheerd via echte EF-migraties (Baseline-squash).
            // De vroegere PendingModelChangesWarning-onderdrukking is verwijderd:
            // model-drift moet nu een fout geven i.p.v. stil genegeerd te worden.
        });

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
        services.AddScoped<IGdprService, GdprService>(); // US-37

        // US-32: authenticatie & autorisatie (singleton — CurrentUser is app-breed)
        services.AddSingleton<IAuthService, AuthService>();

        // US-34: daily automatic backups (SQLite online-backup API)
        services.AddSingleton<IBackupService>(sp => new BackupService(
            connectionString,
            GetDataDirectory(),
            GetBackupOptions(),
            sp.GetRequiredService<ILogger<BackupService>>()));

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

        // UI-thread (dispatcher) excepties: begrens elke onverwachte fout met een toast
        // en voorkom dat de app crasht. De gebruiker ziet dat er iets misging en dat de
        // actie niet is uitgevoerd; de fout wordt naar crash.log geschreven.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            LogToFile(e.Exception);
            ShowErrorToast(e.Exception);
        };

        // ==============================
        // 6️⃣ MAIN WINDOW — open first so the user sees the app immediately
        // ==============================

        MainWindowViewModel? mainVm = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();

        // ==============================
        // 7️⃣ DATABASE INITIALIZATION — runs in background after window is visible.
        //    Previously used GetAwaiter().GetResult() which froze the UI thread and
        //    could deadlock on a slow PostgreSQL connection. Window now opens instantly;
        //    MainWindowViewModel.IsInitializing controls a loading overlay in the AXAML.
        // ==============================

        _ = InitializeInBackgroundAsync(mainVm);

        // Check for updates in the background — never blocks startup, never crashes the app.
        _ = VelopackUpdateChecker.CheckAndNotifyAsync(Services, _logger);
    }
    // ==============================
    // 🚀 BACKGROUND STARTUP
    // ==============================

    /// <summary>
    /// Runs DB initialisation and startup tasks on a background thread after the
    /// window is already visible. Updates <see cref="MainWindowViewModel.IsInitializing"/>
    /// and <see cref="MainWindowViewModel.InitError"/> so the AXAML can show a
    /// loading overlay / error message without blocking the UI thread.
    /// </summary>
    private static async Task InitializeInBackgroundAsync(MainWindowViewModel? mainVm)
    {
        try
        {
            await Task.Run(async () =>
            {
                await InitializeDatabaseAsync(Services);
                await RunStartupTasksAsync(Services);
            });
        }
        catch (Exception ex)
        {
            LogException(ex);
            if (mainVm is not null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                    mainVm.InitError = $"Database kon niet worden gestart: {ex.Message}");
            return;
        }

        if (mainVm is not null)
            await Dispatcher.UIThread.InvokeAsync(() => mainVm.IsInitializing = false);
    }

    // ==============================
    // ⚙️ CONFIGURATION
    // ==============================

    /// <summary>
    /// Reads the DB connection string from appsettings.json if present.
    /// Falls back to a platform-appropriate SQLite path so the app works
    /// on both Windows and macOS without any config file.
    ///
    /// To switch to PostgreSQL on Thursday:
    ///   Place an appsettings.json next to the exe with:
    ///   { "ConnectionStrings": { "Default": "Host=192.168.1.X;Database=quadrodb;Username=quadro;Password=XXXX" } }
    ///   Then swap UseSqlite → UseNpgsql in the DI registration.
    /// </summary>
    private static string GetConnectionString()
    {
        // US-33: full connection string may come from an environment variable
        // (highest priority, useful for testing and server deployments).
        var fromEnv = Environment.GetEnvironmentVariable("QUADRO_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return SecretStore.InjectPassword(fromEnv, GetDataDirectory());

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var cs = config.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(cs))
            {
                // US-33: replace Password=__SECRET__ with the real password from
                // env var / DPAPI secret file. SQLite strings pass through untouched.
                cs = SecretStore.InjectPassword(cs, GetDataDirectory());

                if (SecretStore.HasUnresolvedPlaceholder(cs))
                    File.AppendAllText(_crashLogPath,
                        $"[Config] DB-wachtwoord niet gevonden. Zet omgevingsvariabele {SecretStore.PasswordEnvVar} " +
                        $"of maak het secret-bestand aan met Scripts/set-db-secret.ps1.\n");

                return cs;
            }
        }
        catch (Exception ex)
        {
            // Config load failure is non-fatal — fall back to SQLite.
            // _logger is not yet available here (called during DI setup), so write directly.
            File.AppendAllText(_crashLogPath, $"[Config] appsettings.json lezen mislukt: {ex.Message}\n");
        }

        return GetDefaultSqliteConnectionString();
    }

    /// <summary>
    /// Platform-appropriate user-writable data directory (also holds db.secret):
    ///   Windows → %LOCALAPPDATA%\QuadroApp
    ///   macOS   → ~/Library/Application Support/QuadroApp
    /// </summary>
    private static string GetDataDirectory()
    {
        string dataDir;
        if (OperatingSystem.IsMacOS())
        {
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library", "Application Support", "QuadroApp");
        }
        else
        {
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuadroApp");
        }
        Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    /// <summary>
    /// Returns a SQLite connection string pointing to the correct user-writable
    /// data folder on the current platform:
    ///   Windows → %LOCALAPPDATA%\QuadroApp\quadro.db
    ///   macOS   → ~/Library/Application Support/QuadroApp/quadro.db
    ///
    /// On macOS the app bundle's own folder is read-only, so we can never store
    /// the database next to the exe. This method always uses the OS data folder.
    ///
    /// One-time migration: if an old quadro.db exists next to the exe (from an
    /// earlier install) it is moved to the new location automatically.
    /// </summary>
    /// <summary>
    /// Returns true when the connection string targets PostgreSQL.
    /// The Npgsql connection string always contains "Host=" while SQLite uses "Data Source=".
    /// </summary>
    private static bool IsPostgresConnectionString(string cs)
        => cs.Contains("Host=", StringComparison.OrdinalIgnoreCase);

    private static string GetDefaultSqliteConnectionString()
    {
        var dataDir = GetDataDirectory();
        var newDbPath = Path.Combine(dataDir, "quadro.db");

        // One-time migration from the old location (next to the exe).
        // _logger is not yet available here — write directly to crash.log on failure.
        var oldDbPath = Path.Combine(AppContext.BaseDirectory, "quadro.db");
        if (!File.Exists(newDbPath) && File.Exists(oldDbPath))
        {
            try
            {
                File.Move(oldDbPath, newDbPath);
            }
            catch (Exception ex)
            {
                File.AppendAllText(_crashLogPath, $"[DB] Kon quadro.db niet verplaatsen: {ex.Message}\n");
            }
        }

        return $"Data Source={newDbPath}";
    }

    /// <summary>Schrijf een exception naar crash.log (faalt nooit zelf).</summary>
    private static void LogToFile(Exception? ex)
    {
        if (ex == null) return;
        try { File.AppendAllText(_crashLogPath, $"[{DateTime.Now}] {ex}\n\n"); }
        catch { /* logging mag de app nooit laten vallen */ }
    }

    /// <summary>Toon een nette foutmelding-toast i.p.v. te crashen (UI-thread veilig).</summary>
    private static void ShowErrorToast(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var sp = Services;
            var toast = sp?.GetService<IToastService>();
            if (toast is null) return;

            var detail = (ex.InnerException ?? ex).Message;
            void Show() => toast.Error($"Er ging iets mis — de actie werd niet uitgevoerd. ({detail})");

            if (Dispatcher.UIThread.CheckAccess()) Show();
            else Dispatcher.UIThread.Post(Show);
        }
        catch { /* een fout-handler mag nooit zelf falen */ }
    }

    private static void LogException(Exception? ex)
    {
        if (ex == null) return;

        LogToFile(ex);

        // US-31: ook naar het gestructureerde log (wanneer DI al opgestart is).
        try { _logger?.LogError(ex, "Onafgevangen exceptie: {Message}", ex.Message); }
        catch { /* logging mag nooit zelf crashen */ }

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

        if (db.Database.IsNpgsql())
            await InitializePostgresDatabaseAsync(db);
        else
            await InitializeSqliteDatabaseAsync(db);

        // Seed static reference data (idempotent — safe to run on every launch)
        DbSeeder.SeedDemoData(db);

        _logger.LogInformation("[DB] Klanten={K}, TypeLijsten={L}, Offertes={O}",
            await db.Klanten.CountAsync(),
            await db.TypeLijsten.CountAsync(),
            await db.Offertes.CountAsync());
    }

    /// <summary>
    /// PostgreSQL initialisation — always a fresh install (data is migrated from SQLite
    /// using the migration script on Thursday). EnsureCreatedAsync builds the entire
    /// schema from the EF model using PostgreSQL-native syntax (SERIAL, BOOLEAN, etc.).
    /// No migration history is needed because we never upgrade an existing PostgreSQL DB
    /// via EF migrations — schema changes are applied as raw SQL patches instead.
    /// </summary>
    private static async Task InitializePostgresDatabaseAsync(AppDbContext db)
    {
        try
        {
            await db.Database.EnsureCreatedAsync();
            _logger.LogInformation("[DB] PostgreSQL schema gecontroleerd/aangemaakt.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB] Fout bij PostgreSQL initialisatie: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// US-30: SQLite initialisation via echte EF-migraties.
    /// Fresh DB → MigrateAsync (Baseline). Bestaande DB → healer-patches +
    /// Baseline-markering + MigrateAsync. Zie SqliteSchemaPatcher.
    /// </summary>
    private static async Task InitializeSqliteDatabaseAsync(AppDbContext db)
    {
        await SqliteSchemaPatcher.PatchAsync(db, _logger);
    }


    private static async Task RunStartupTasksAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();

        _logger.LogInformation("[Startup] Refreshing voorraad alerts...");
        await stockService.RefreshAlertsAsync();

        // US-32: standaard admin aanmaken wanneer er nog geen gebruikers zijn.
        var auth = provider.GetRequiredService<IAuthService>();
        await auth.SeedDefaultAdminAsync();

        // US-34: daily backup — runs after DB init, never blocks or crashes startup.
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        await backupService.RunDailyBackupAsync();
    }

    /// <summary>US-31: leest één configuratiewaarde uit appsettings.json (null bij afwezigheid/fout).</summary>
    private static string? GetConfigValue(string key)
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
            return config[key];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>US-34: reads the optional Backup section from appsettings.json.</summary>
    private static Service.Backup.BackupOptions GetBackupOptions()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var section = config.GetSection("Backup");
            return new Service.Backup.BackupOptions
            {
                Directory = section["Directory"],
                RetentionDays = int.TryParse(section["RetentionDays"], out var days) ? days : 30
            };
        }
        catch
        {
            return new Service.Backup.BackupOptions();
        }
    }






}
