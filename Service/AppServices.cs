using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Linq;

public static class AppServices
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Gemakkelijk toegang tot je DbContext
    public static AppDbContext Db => Services.GetRequiredService<AppDbContext>();

    public static void Init()
    {
        var services = new ServiceCollection();

        // >>> SQLite connectiestring
        var connectionString = "Data Source=quadro.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // hier eventueel later nog andere services registreren

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ✅ volledig leegmaken
        db.Database.EnsureDeleted();

        // ✅ opnieuw aanmaken + migraties toepassen
        db.Database.Migrate();

        DbSeeder.SeedDemoData(db);

    }
}
