using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuadroApp.Data;

/// <summary>
/// SQLite design-time factory voor <c>dotnet ef</c>. De SQLite-migraties leven in deze
/// assembly (<c>QuadroApp.Data</c>). Gebruik als startup-project:
/// <c>dotnet ef migrations add X --project QuadroApp.Data --startup-project QuadroApp.Data</c>.
///
/// De PostgreSQL-migraties hebben hun eigen factory (<c>NpgsqlDesignTimeFactory</c>) in het
/// project <c>QuadroApp.Migrations.Npgsql</c>; <c>dotnet ef</c> scant enkel de startup-assembly,
/// dus elke provider heeft zijn factory in de assembly die je als startup opgeeft.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=quadro.db", o => o.MigrationsAssembly("QuadroApp.Data"))
            .Options;

        return new AppDbContext(options);
    }
}
