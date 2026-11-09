using System.Linq;
using QuadroApp.Service;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>crash.log vermeldt de bron, het scherm en de laatste klikken.</summary>
[Collection("CrashContext")]
public class CrashContextTests
{
    [Fact]
    public void Beschrijf_ToontBronSchermEnLaatsteActies()
    {
        CrashContext.Leeg();
        CrashContext.NoteerScherm(new object());
        CrashContext.NoteerActie("klik 'Verwijderen' (Ontvangst)");

        var tekst = CrashContext.Beschrijf("TaskScheduler.UnobservedTaskException");

        Assert.Contains("Bron: TaskScheduler.UnobservedTaskException", tekst);
        Assert.Contains("Scherm: Object", tekst);
        Assert.Contains("klik 'Verwijderen' (Ontvangst)", tekst);
    }

    [Fact]
    public void Beschrijf_HoudtEnkelDeLaatsteAchtActiesBij()
    {
        CrashContext.Leeg();
        for (var i = 1; i <= 10; i++) CrashContext.NoteerActie($"actie {i}");

        var regels = CrashContext.Beschrijf("test").Split('\n').Where(r => r.Contains("actie ")).ToList();

        Assert.Equal(8, regels.Count);
        Assert.EndsWith("actie 3", regels.First());
        Assert.EndsWith("actie 10", regels.Last());
    }

    [Fact]
    public void Beschrijf_ZonderGegevens_ZegtOnbekend()
    {
        CrashContext.Leeg();
        var tekst = CrashContext.Beschrijf("test");
        Assert.Contains("Scherm: (onbekend)", tekst);
        Assert.Contains("Laatste acties: (geen)", tekst);
    }

    [Fact]
    public void NoteerActie_HoudtEenRegel_ZonderNieuweRegelsOfNul()
    {
        CrashContext.Leeg();
        CrashContext.NoteerActie("klik 'Jan\r\nHash: x\0'");
        var tekst = CrashContext.Beschrijf("test");
        Assert.DoesNotContain("\nHash:", tekst);
        Assert.False(tekst.Contains((char)0));
        Assert.Equal(1, tekst.Split('\n').Count(r => r.StartsWith("  [")));
    }
}
