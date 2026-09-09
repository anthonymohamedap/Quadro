using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-66 — DbContext round-trip voor <see cref="WinkelVerkoop"/>: bewaakt dat
/// <see cref="Betaalwijze"/> correct persisteert via <c>HasConversion&lt;string&gt;()</c> (SQLite).
/// Er bestaat geen precedent van ViewModel-tests voor dit type eenvoudig CRUD-scherm
/// (<c>KantKlaarKaderenViewModel</c> heeft er ook geen) — deze test dekt enkel de dataslaag.</summary>
public class WinkelVerkoopTests
{
    [Fact]
    public async Task WinkelVerkoop_persisteert_en_herlaadt_Betaalwijze_via_string_conversie()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();

        var verkoop = new WinkelVerkoop
        {
            Datum = new DateTime(2026, 9, 10),
            Omschrijving = "Fotolijst 10x15 wit",
            Aantal = 2m,
            PrijsInclBtw = 12.50m,
            BtwPct = 21m,
            Betaalwijze = Betaalwijze.Bancontact
        };

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            db.WinkelVerkopen.Add(verkoop);
            await db.SaveChangesAsync();
        }

        // Nieuwe context (los van de tracking hierboven) om een echte herlaad-round-trip te forceren.
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var herladen = await db.WinkelVerkopen.AsNoTracking().SingleAsync(v => v.Id == verkoop.Id);

            Assert.Equal(Betaalwijze.Bancontact, herladen.Betaalwijze);
            Assert.Equal("Fotolijst 10x15 wit", herladen.Omschrijving);
            Assert.Equal(2m, herladen.Aantal);
            Assert.Equal(12.50m, herladen.PrijsInclBtw);
            Assert.Equal(21m, herladen.BtwPct);
            Assert.Equal(25.00m, herladen.TotaalInclBtw);

            // De kolom slaat de enum als string op (HasConversion<string>()), niet als int.
            var raw = await db.Database
                .SqlQueryRaw<string>("SELECT Betaalwijze AS Value FROM WinkelVerkopen WHERE Id = {0}", verkoop.Id)
                .SingleAsync();
            Assert.Equal("Bancontact", raw);
        }
    }
}
