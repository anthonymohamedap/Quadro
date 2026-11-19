using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Auditlog: regels ouder dan 30 dagen worden bij het opstarten verwijderd (afspraak 19/11/2026).</summary>
public class AuditOpruimenTests
{
    [Fact]
    public async Task Regels_ouder_dan_30_dagen_verdwijnen_de_rest_blijft()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var nu = new DateTime(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            foreach (var dagenOud in new[] { 0, 29, 30, 31, 90 })
                db.AuditLogs.Add(new AuditLog
                {
                    Tijdstip = nu.AddDays(-dagenOud).AddMinutes(dagenOud == 30 ? 1 : 0),   // net binnen 30 dagen
                    Gebruiker = "veerle", EntiteitType = "Offerte", EntiteitId = dagenOud.ToString(),
                    Actie = "Gewijzigd", Wijzigingen = "{}"
                });
            await db.SaveChangesAsync();
        }

        var weg = await AuditService.RuimOudeRegelsOpAsync(scope.Factory, nu);

        Assert.Equal(2, weg);   // 31 en 90 dagen oud
        await using var check = await scope.Factory.CreateDbContextAsync();
        var over = await check.AuditLogs.Select(a => a.EntiteitId).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { "0", "29", "30" }, over);
    }
}
