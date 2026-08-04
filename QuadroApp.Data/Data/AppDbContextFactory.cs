using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuadroApp.Data;

/// <summary>
/// Design-time factory voor <c>dotnet ef</c>. Kiest de provider via de omgevingsvariabele
/// <c>EF_PROVIDER</c> zodat één factory beide providers bedient en de juiste
/// <c>MigrationsAssembly</c> zet:
///   • <c>EF_PROVIDER=Npgsql</c> → PostgreSQL, migraties in <c>QuadroApp.Migrations.Npgsql</c>
///     (connection string uit <c>QUADRO_PG_DESIGN_CS</c>, met een dev-fallback);
///   • anders → SQLite, migraties in <c>QuadroApp.Data</c> (deze assembly, waar ze nu leven).
///
/// Dit raakt alleen de migratie-tooling; de runtime-configuratie zit in de app (App.axaml.cs).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER");
        var b = new DbContextOptionsBuilder<AppDbContext>();

        if (string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var cs = Environment.GetEnvironmentVariable("QUADRO_PG_DESIGN_CS")
                     ?? "Host=localhost;Port=5432;Database=quadro_migdev;Username=postgres;Password=postgres";
            b.UseNpgsql(cs, o => o.MigrationsAssembly("QuadroApp.Migrations.Npgsql"));
        }
        else
        {
            b.UseSqlite("Data Source=quadro.db",
                        o => o.MigrationsAssembly("QuadroApp.Data"));
        }

        return new AppDbContext(b.Options);
    }
}
