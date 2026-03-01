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
using QuadroApp.Service.Import.Enterprise;
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
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddTransient<IKlantDialogService, KlantDialogService>();
        services.AddTransient<ILijstDialogService, LijstDialogService>();

        // ==============================
        // 4️⃣ DOMAIN SERVICES
        // ==============================

        services.AddScoped<IAfwerkingenService, AfwerkingenService>();
        services.AddScoped<IWorkflowService, QuadroApp.Service.WorkflowService>();
        services.AddScoped<IOfferteWorkflowService, OfferteWorkflowService>();
        services.AddScoped<IWerkBonWorkflowService, WerkBonWorkflowService>();
        services.AddScoped<IFactuurWorkflowService, FactuurWorkflowService>();
        services.AddScoped<IFactuurExportService, FactuurExportService>();
        services.AddScoped<IFactuurExporter, PdfFactuurExporter>();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<PricingEngine>();
        services.AddSingleton<IPricingSettingsProvider, PricingSettingsProvider>();
        services.AddSingleton<IPricingService, PricingService>();

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

        // Import (legacy path; candidate for removal after runtime verification)
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
        services.AddTransient<LeveranciersViewModel>();
        services.AddTransient<AfwerkingenViewModel>();
        services.AddTransient<OffertesLijstViewModel>();
        services.AddTransient<OfferteViewModel>();
        services.AddTransient<KlantDetailViewModel>();
        services.AddTransient<WerkBonLijstViewModel>();
        services.AddTransient<FacturenViewModel>();

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

        Console.WriteLine("[DB] Resetting demo database...");
        await db.Database.EnsureCreatedAsync();

        // Seed data (single source of truth)
        DbSeeder.SeedDemoData(db);

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




}
