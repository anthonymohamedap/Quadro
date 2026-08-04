using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-43 — ExecuteWithRetryAsync: de transactie-body committeert bij succes,
/// rolt terug bij een fout vóór commit, en de generieke variant geeft de
/// body-waarde terug. (De retry zelf is niet-herhalend op SQLite; het
/// transient-retry-gedrag wordt op PostgreSQL gevalideerd.)
/// </summary>
public class DbExecutionExtensionsTests
{
    [Fact]
    public async Task Commit_bij_succes_persisteert()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await scope.Factory.CreateDbContextAsync();

        await db.ExecuteWithRetryAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Klanten.Add(new Klant { Voornaam = "Test", Achternaam = "Persoon" });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        await using var check = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await check.Klanten.CountAsync());
    }

    [Fact]
    public async Task Fout_zonder_commit_rolt_terug()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await scope.Factory.CreateDbContextAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await db.ExecuteWithRetryAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                db.Klanten.Add(new Klant { Voornaam = "Rollback", Achternaam = "Persoon" });
                await db.SaveChangesAsync();
                throw new InvalidOperationException("boom vóór commit");
            }));

        await using var check = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(0, await check.Klanten.CountAsync());
    }

    [Fact]
    public async Task Generieke_variant_geeft_body_waarde_terug()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await scope.Factory.CreateDbContextAsync();

        var nieuweId = await db.ExecuteWithRetryAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var klant = new Klant { Voornaam = "Waarde", Achternaam = "Persoon" };
            db.Klanten.Add(klant);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return klant.Id;
        });

        Assert.True(nieuweId > 0);
    }
}
