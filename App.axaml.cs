using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
using QuadroApp.Service.Toast;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
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

        // 🔹 Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
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
        _ = CheckForUpdatesAsync();
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
    // 🔄 AUTO-UPDATE (Velopack)
    // ==============================

    /// <summary>
    /// Silently checks GitHub Releases for a newer version.
    /// Downloads it in the background and shows a toast when ready.
    /// The update is applied on the next app restart — nothing interrupts the user.
    /// </summary>
    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            // ── Update-bron ────────────────────────────────────────────────────────────
            // Productie : GitHub Releases (standaard).
            // Lokale test: definieer LOCAL_TEST bij het bouwen — test-velopack-local.ps1
            //              doet dit automatisch zodat de app http://localhost:8080 gebruikt.
            // Terugzetten: git checkout App.axaml.cs
#if LOCAL_TEST
            const string repoUrl = "http://localhost:8080";
#else
            const string repoUrl = "https://github.com/anthonymohamedap/Quadro";
#endif

            var source = new GithubSource(repoUrl, accessToken: null, prerelease: false);
            var channel = OperatingSystem.IsMacOS() ? "osx" : "win";
            var mgr = new UpdateManager(source, new UpdateOptions { ExplicitChannel = channel });

            // Not running as a Velopack-installed app (e.g. dev machine) → skip silently.
            if (!mgr.IsInstalled) return;

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null) return;

            _logger.LogInformation("[Update] Nieuwe versie beschikbaar: {Version}", newVersion.TargetFullRelease.Version);

            await mgr.DownloadUpdatesAsync(newVersion);

            _logger.LogInformation("[Update] Download klaar. Wordt toegepast bij volgende herstart.");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var toast = Services.GetService<IToastService>();
                toast?.Info("🔄 Update gedownload.", "Herstart nu", () => mgr.ApplyUpdatesAndRestart(newVersion));
            });
        }
        catch (System.Net.Http.HttpRequestException httpEx)
        {
            // Netwerk niet beschikbaar, DNS-fout, 404, … — allemaal niet-kritiek.
            _logger.LogInformation("[Update] Update-controle overgeslagen (netwerk): {Message}", httpEx.Message);
        }
        catch (Exception ex)
        {
            // Update check should never crash the app — only log, never write to crash.log.
            _logger.LogWarning(ex, "[Update] Update-controle mislukt (niet kritiek): {Message}", ex.Message);
        }
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
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var cs = config.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(cs))
                return cs;
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
        // Platform-appropriate data directory
        string dataDir;
        if (OperatingSystem.IsMacOS())
        {
            // macOS convention: ~/Library/Application Support/QuadroApp
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library", "Application Support", "QuadroApp");
        }
        else
        {
            // Windows: C:\Users\<user>\AppData\Local\QuadroApp
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuadroApp");
        }

        Directory.CreateDirectory(dataDir);
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

    private static void LogException(Exception? ex)
    {
        if (ex == null) return;

        var text = $"[{DateTime.Now}] {ex}\n\n";
        File.AppendAllText(_crashLogPath, text);

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
    /// SQLite initialisation — existing path with preExistingMigrations hack and
    /// defensive ALTER TABLE patches. Unchanged so existing installs keep working.
    /// </summary>
    private static async Task InitializeSqliteDatabaseAsync(AppDbContext db)
    {
        // Ensure DB file exists (new installs)
        await db.Database.EnsureCreatedAsync();

        // Apply pending migrations safely.
        await ApplyPendingMigrationsAsync(db);
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

        // ── Stap 1b: Schema-patches voor kolommen die mogelijk ontbreken op oudere DBs ─
        // PRAGMA table_info check first: no ALTER TABLE attempted when column already exists,
        // so EF Core never logs a scary "fail:" line for a harmless duplicate-column error.

        var conn = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        async Task<bool> ColumnExistsAsync(string table, string column)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(result) > 0;
        }

        // AddAfwerkingsKleur (20260323120500) — Kleur op AfwerkingsOpties
        if (!await ColumnExistsAsync("AfwerkingsOpties", "Kleur"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AfwerkingsOpties\" ADD COLUMN \"Kleur\" TEXT NOT NULL DEFAULT 'Standaard'");

        // AddAfwerkingsKleur (20260323120500) — uniek index updaten naar versie mét Kleur
        // SQLite error 1 = SQLITE_ERROR; "no such index" is the only expected failure for DROP.
        try { await db.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer\""); }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase))
        { /* index bestond al niet — prima */ }
        // IF NOT EXISTS makes the CREATE idempotent; no catch needed.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AfwerkingsOpties_AfwerkingsGroepId_Volgnummer_Kleur\" " +
            "ON \"AfwerkingsOpties\" (\"AfwerkingsGroepId\", \"Volgnummer\", \"Kleur\")");

        // AddTitelToOfferteRegel (20260321121423) — Titel op OfferteRegels
        if (!await ColumnExistsAsync("OfferteRegels", "Titel"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"Titel\" TEXT NULL");

        // AddRowVersionToOfferte (20260506000000) — optimistic concurrency token
        if (!await ColumnExistsAsync("Offertes", "RowVersion"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"RowVersion\" BLOB NULL");

        // AddGeplandeDatumToFactuur (20260506130000)
        if (!await ColumnExistsAsync("Facturen", "GeplandeDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"GeplandeDatum\" TEXT NULL");

        // AddAfwerkingsVariant (20260507110000)
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""AfwerkingsVarianten"" (
    ""Id""                  INTEGER NOT NULL CONSTRAINT ""PK_AfwerkingsVarianten"" PRIMARY KEY AUTOINCREMENT,
    ""AfwerkingsOptieId""   INTEGER NOT NULL,
    ""Beschrijving""        TEXT    NOT NULL,
    ""Kleur""               TEXT    NULL,
    ""VariantCode""         TEXT    NULL,
    ""IsStandaard""         INTEGER NOT NULL DEFAULT 0,
    ""IsActief""            INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT ""FK_AfwerkingsVarianten_AfwerkingsOpties_AfwerkingsOptieId""
        FOREIGN KEY (""AfwerkingsOptieId"") REFERENCES ""AfwerkingsOpties"" (""Id"") ON DELETE CASCADE
);");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AfwerkingsVarianten_OptieId_Beschrijving""
    ON ""AfwerkingsVarianten"" (""AfwerkingsOptieId"", ""Beschrijving"");");
        // Auto-migrate: maak 1 variant per bestaande optie als nog niet aangemaakt
        await db.Database.ExecuteSqlRawAsync(@"
INSERT OR IGNORE INTO ""AfwerkingsVarianten"" (""AfwerkingsOptieId"", ""Beschrijving"", ""IsStandaard"", ""IsActief"")
SELECT ""Id"", COALESCE(NULLIF(TRIM(""Kleur""), ''), 'Standaard'), 1, 1
FROM ""AfwerkingsOpties""
WHERE NOT EXISTS (
    SELECT 1 FROM ""AfwerkingsVarianten"" v WHERE v.""AfwerkingsOptieId"" = ""AfwerkingsOpties"".""Id""
);");

        // AddAfhaalDatum (20260507100000)
        if (!await ColumnExistsAsync("Offertes", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Offertes\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");
        if (!await ColumnExistsAsync("Facturen", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Facturen\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");

        // AddAfhaalDatumToOfferteRegel (20260507130000)
        if (!await ColumnExistsAsync("OfferteRegels", "AfhaalDatum"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"OfferteRegels\" ADD COLUMN \"AfhaalDatum\" TEXT NULL");

        // AddBestelVormToBestellijn (20260507093626)
        if (!await ColumnExistsAsync("LeverancierBestelLijnen", "BestelVorm"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"LeverancierBestelLijnen\" ADD COLUMN \"BestelVorm\" INTEGER NOT NULL DEFAULT 0");

        // AddVoorraadAlerts (20260506120000) — ensure table exists before HomeViewModel loads
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""VoorraadAlerts"" (
    ""Id""                    INTEGER NOT NULL CONSTRAINT ""PK_VoorraadAlerts"" PRIMARY KEY AUTOINCREMENT,
    ""TypeLijstId""           INTEGER NULL,
    ""AlertType""             TEXT    NOT NULL,
    ""Status""                TEXT    NOT NULL,
    ""AangemaaktOp""          TEXT    NOT NULL,
    ""LaatstHerinnerdOp""     TEXT    NULL,
    ""VolgendeHerinneringOp"" TEXT    NULL,
    ""BronReferentie""        TEXT    NULL,
    ""Bericht""               TEXT    NOT NULL,
    CONSTRAINT ""FK_VoorraadAlerts_TypeLijsten_TypeLijstId""
        FOREIGN KEY (""TypeLijstId"") REFERENCES ""TypeLijsten"" (""Id"") ON DELETE SET NULL
)");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_VoorraadAlerts_TypeLijstId"" ON ""VoorraadAlerts"" (""TypeLijstId"")");

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
            "20260506000000_AddRowVersionToOfferte",
            "20260506120000_AddVoorraadAlerts",
            "20260506130000_AddGeplandeDatumToFactuur",
            "20260507100000_AddAfhaalDatum",
            "20260507110000_AddAfwerkingsVariant",
            "20260507011133_tag10",
            "20260507023057_date",
            "20260507130000_AddAfhaalDatumToOfferteRegel",
            "20260507093626_AddBestelVormToBestellijn",
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
