using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-41 (stap H) — drift-bewaking: het EF-model mag niet afwijken van de laatste migratie.
/// Deze test dekt de SQLite-provider (migraties in QuadroApp.Data) en draait in CI zonder
/// PostgreSQL. Voor Npgsql draai je dezelfde controle met:
///   dotnet ef migrations has-pending-model-changes
///     --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql
/// </summary>
public class ModelDriftTests
{
    [Fact]
    public async Task Sqlite_model_heeft_geen_pending_changes()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await scope.Factory.CreateDbContextAsync();

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "Het SQLite-model wijkt af van de laatste migratie. " +
            "Voeg een EF-migratie toe (dotnet ef migrations add ...) i.p.v. het schema handmatig te wijzigen.");
    }
}
