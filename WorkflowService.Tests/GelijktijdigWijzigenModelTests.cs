using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Linq;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// A-04 — gelijktijdig wijzigen op PostgreSQL. Het model wordt opgebouwd zonder server (geen verbinding nodig).
/// De echte werking (melding bij twee pc's, bewaren via een kopie) is nagekeken op PostgreSQL 16; zie de PR.
/// </summary>
public class GelijktijdigWijzigenModelTests
{
    private static readonly Type[] Bewaakt =
        { typeof(Offerte), typeof(WerkTaak), typeof(WerkBon), typeof(Bestelbon), typeof(TypeLijst) };

    [Fact]
    public void Op_PostgreSQL_is_xmin_het_token_als_gewone_eigenschap()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only").Options);

        foreach (var type in Bewaakt)
        {
            var et = db.Model.FindEntityType(type)!;
            var versie = et.FindProperty(nameof(Offerte.PgVersie));
            Assert.NotNull(versie);
            Assert.False(versie!.IsShadowProperty(), $"{type.Name}: versie moet meereizen met een kopie");
            Assert.True(versie.IsConcurrencyToken, $"{type.Name}: geen token");
            Assert.Equal("xmin", versie.GetColumnName());
            Assert.False(et.FindProperty("RowVersion")!.IsConcurrencyToken, $"{type.Name}: RowVersion mag op PostgreSQL geen token zijn");
        }
    }

    [Fact]
    public void Op_SQLite_bestaat_PgVersie_niet_en_blijft_RowVersion_het_token()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options);

        foreach (var type in Bewaakt)
        {
            var et = db.Model.FindEntityType(type)!;
            Assert.Null(et.FindProperty(nameof(Offerte.PgVersie)));
            Assert.True(et.FindProperty("RowVersion")!.IsConcurrencyToken, $"{type.Name}: RowVersion moet op SQLite het token zijn");
        }
    }
}
