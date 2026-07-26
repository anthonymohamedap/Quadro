using System;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Service.Concurrency;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>REL-03 — nette gebruikersmelding bij gelijktijdige wijzigingen.</summary>
public class ConcurrencyUxTests
{
    [Fact]
    public void Melding_GelijktijdigeWijziging_ReturnsEigenTekst()
    {
        var ex = new GelijktijdigeWijzigingException("De voorraad werd intussen gewijzigd.");
        Assert.Equal("De voorraad werd intussen gewijzigd.", ConcurrencyUx.Melding(ex));
        Assert.True(ConcurrencyUx.IsConflict(ex));
    }

    [Fact]
    public void Melding_DbUpdateConcurrency_ReturnsStandaardmelding()
    {
        var ex = new DbUpdateConcurrencyException("lelijke EF-tekst");
        Assert.Equal(ConcurrencyUx.StandaardMelding, ConcurrencyUx.Melding(ex));
    }

    [Fact]
    public void Melding_ConflictAlsInnerException_WordtGevonden()
    {
        var inner = new DbUpdateConcurrencyException("lelijke EF-tekst");
        var outer = new InvalidOperationException("wrapper", inner);
        Assert.Equal(ConcurrencyUx.StandaardMelding, ConcurrencyUx.Melding(outer));
    }

    [Fact]
    public void Melding_GewoneFout_ReturnsNull()
    {
        Assert.Null(ConcurrencyUx.Melding(new InvalidOperationException("gewone fout")));
        Assert.Null(ConcurrencyUx.Melding(null));
        Assert.False(ConcurrencyUx.IsConflict(new Exception("x")));
    }
}
