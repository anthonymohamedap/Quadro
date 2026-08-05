using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuadroApp.Data;

namespace QuadroApp.Migrations.Npgsql;

/// <summary>
/// PostgreSQL design-time factory voor <c>dotnet ef</c>. Leeft in dit project zodat EF ze vindt
/// wanneer je dit project als startup gebruikt:
///
/// <code>
/// dotnet ef migrations add X --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql
/// </code>
///
/// Connection string uit <c>QUADRO_PG_DESIGN_CS</c> (met een wegwerp-dev-fallback). Voor
/// <c>migrations add</c> is geen draaiende DB nodig; voor <c>database update</c>/<c>script</c> wel.
/// </summary>
public class NpgsqlDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("QUADRO_PG_DESIGN_CS")
                 ?? "Host=localhost;Port=5432;Database=quadro_migdev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs, o => o.MigrationsAssembly("QuadroApp.Migrations.Npgsql"))
            .Options;

        return new AppDbContext(options);
    }
}
