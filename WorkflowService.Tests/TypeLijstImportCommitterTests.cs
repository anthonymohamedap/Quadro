using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

public class TypeLijstImportCommitterTests
{
    [Fact]
    public async Task Commit_DoesNotOverwritePricingFields()
    {
        var factory = new PooledDbContextFactory<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var lev = new Leverancier { Naam = "LEV" };
            var existing = new TypeLijst
            {
                Artikelnummer = "ART-1",
                Levcode = "A",
                Leverancier = lev,
                BreedteCm = 10,
                Soort = "ALU",
                PrijsPerMeter = 99m,
                VasteKost = 12m,
                WerkMinuten = 20,
                VoorraadMeter = 1m,
                MinimumVoorraad = 1m,
                InventarisKost = 1m
            };
            db.TypeLijsten.Add(existing);
            await db.SaveChangesAsync();

            var parsed = new TypeLijst
            {
                Artikelnummer = "ART-1",
                Levcode = "B",
                Leverancier = new Leverancier { Naam = "LEV" },
                BreedteCm = 11,
                Soort = "HOU",
                VoorraadMeter = 5m,
                MinimumVoorraad = 2m,
                InventarisKost = 3m
            };

            var rows = new List<ImportRowResult<TypeLijst>> { new() { RowNumber = 2, Parsed = parsed } };
            var sut = new TypeLijstImportCommitter();
            await sut.CommitAsync(rows, db, CancellationToken.None);
        }

        await using (var assertDb = await factory.CreateDbContextAsync())
        {
            var saved = await assertDb.TypeLijsten.SingleAsync();
            Assert.Equal(99m, saved.PrijsPerMeter);
            Assert.Equal(12m, saved.VasteKost);
            Assert.Equal(20, saved.WerkMinuten);
            Assert.Equal(5m, saved.VoorraadMeter);
        }
    }
}
