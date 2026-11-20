using System;
using QuadroApp.Views;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Leeg of fout getal in een getalveld: duidelijke tekst i.p.v. de technische fout.</summary>
public class GetalInvoerFoutTests
{
    [Fact]
    public void LeegVeld_GeeftDuidelijkeTekst() =>
        Assert.Equal(GetalInvoerFout.Tekst,
            GetalInvoerFout.Vertaal(new InvalidCastException("Could not convert '(null)' (null) to System.Int32.")));

    [Fact]
    public void LettersOfTeGroot_GeeftDuidelijkeTekst()
    {
        Assert.Equal(GetalInvoerFout.Tekst, GetalInvoerFout.Vertaal(new FormatException()));
        Assert.Equal(GetalInvoerFout.Tekst, GetalInvoerFout.Vertaal(new OverflowException()));
        Assert.Equal(GetalInvoerFout.Tekst, GetalInvoerFout.Vertaal(new Exception("binding", new InvalidCastException())));
    }

    [Fact]
    public void EigenValidatie_BlijftOngewijzigd()
    {
        Assert.Equal("Breedte moet groter zijn dan 0.", GetalInvoerFout.Vertaal("Breedte moet groter zijn dan 0."));
        var andere = new InvalidOperationException("iets anders");
        Assert.Same(andere, GetalInvoerFout.Vertaal(andere));
    }
}
