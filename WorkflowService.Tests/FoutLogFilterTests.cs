using QuadroApp.Service.Foutlog;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>FoutLogFilter: gevoelige tekst maskeren, stackframes intact laten, velden inkorten.</summary>
public class FoutLogFilterTests
{
    [Fact]
    public void Filter_LaatStackframeIntact()
    {
        var frame = @"   at QuadroApp.X.Y(Int32 id) in C:\a\B.cs:line 123";
        Assert.Equal(frame, FoutLogFilter.Filter(frame));
    }

    [Fact]
    public void Filter_NullOfLeeg_GeeftLeeg()
    {
        Assert.Equal("", FoutLogFilter.Filter(null));
        Assert.Equal("", FoutLogFilter.Filter(""));
    }

    [Fact]
    public void Filter_ConnectionString_MaskeertWachtwoordEnGebruiker()
    {
        var r = FoutLogFilter.Filter("Host=db;Username=quadro;Password=Geheim123;Port=5432");
        Assert.DoesNotContain("Geheim123", r);
        Assert.DoesNotContain("quadro;", r);
        Assert.Contains("Password=***", r);
        Assert.Contains("Port=5432", r);
    }

    [Theory]
    [InlineData("mail jan.peeters@test.be nu", "jan.peeters@test.be", "[e-mail]")]
    [InlineData("NewPassword=geheim123 fout", "geheim123", "***")]
    [InlineData("OldPassword: geheim123", "geheim123", "***")]
    [InlineData("totaal € 1.234,50 betaald", "1.234,50", "[bedrag]")]
    [InlineData("totaal 12,50 EUR betaald", "12,50", "[bedrag]")]
    [InlineData("rekening BE68 5390 0754 7034 nu", "5390", "[iban]")]
    [InlineData("rekening BE68539007547034 nu", "539007547034", "[iban]")]
    [InlineData("bel 0472 12 34 56 nu", "0472", "[tel]")]
    [InlineData("bel +32 472 12 34 56 nu", "472 12", "[tel]")]
    [InlineData("wachtwoord: abc123 ok", "abc123", "wachtwoord: ***")]
    public void Filter_Maskeert(string invoer, string verdwenen, string verwacht)
    {
        var r = FoutLogFilter.Filter(invoer);
        Assert.DoesNotContain(verdwenen, r);
        Assert.Contains(verwacht, r);
    }

    [Fact]
    public void Filter_MaskeertInMeerregeligeStacktrace_MaarLaatFramesStaan()
    {
        var tekst = "System.Exception: fout voor jan@x.be met Password=abc;\n   at A.B(Int32 id) in C:\\a\\B.cs:line 123";
        var r = FoutLogFilter.Filter(tekst);
        Assert.DoesNotContain("jan@x.be", r);
        Assert.DoesNotContain("abc", r);
        Assert.Contains("   at A.B(Int32 id) in C:\\a\\B.cs:line 123", r);
    }

    [Fact]
    public void Kort_KortIn_TotMaxInclusiefPuntjes()
    {
        var r = FoutLogFilter.Kort(new string('x', 5000), FoutLogFilter.MaxMelding);
        Assert.Equal(FoutLogFilter.MaxMelding, r.Length);
        Assert.EndsWith("…", r);
    }

    [Fact]
    public void Kort_KorteTekst_BlijftGelijk()
    {
        Assert.Equal("abc", FoutLogFilter.Kort("abc", 10));
        Assert.Equal("", FoutLogFilter.Kort(null, 10));
    }

    [Fact]
    public void Kort_SplitstGeenSurrogaatPaar()
    {
        var tekst = new string('a', 8) + "😀😀😀";
        var r = FoutLogFilter.Kort(tekst, 10);
        Assert.True(r.Length <= 10);
        Assert.False(char.IsHighSurrogate(r[^2]));
    }

    [Fact]
    public void FilterKort_FilterEnKort()
    {
        var r = FoutLogFilter.FilterKort("a@b.be " + new string('y', 3000), 100);
        Assert.Equal(100, r.Length);
        Assert.StartsWith("[e-mail]", r);
    }

    [Theory]
    [InlineData("duplicate key. Key (Naam)=(Jansen) already exists.", "Jansen", "Key (Naam)=(***)")]
    [InlineData("23505\nDetail: Key (Naam)=(Peeters) bestaat\nvolgende", "Peeters", "Detail: ***")]
    [InlineData("Totaal 1234,56 besteld", "1234,56", "[bedrag]")]
    [InlineData("bedrag 12.50 nu", "12.50", "[bedrag]")]
    [InlineData("Password='a;b';Port=1", "a;b", "Password=***")]
    [InlineData("Password=\"geheim\";Port=1", "geheim", "Password=***")]
    [InlineData("{\"password\":\"geheim\",\"x\":1}", "geheim", "\"password\":***")]
    [InlineData("rekening NL91 ABNA 0417 1643 00 nu", "ABNA", "[iban]")]
    [InlineData("rekening DE89370400440532013000 nu", "0532013000", "[iban]")]
    public void Filter_Gaten_Dicht(string invoer, string weg, string verwacht)
    {
        var r = FoutLogFilter.Filter(invoer);
        Assert.DoesNotContain(weg, r);
        Assert.Contains(verwacht, r);
    }

    [Theory]
    [InlineData("Quadro versie 1.1.0 draait")]
    [InlineData("Microsoft.Data.Sqlite, Version=9.0.1.0, Culture=neutral")]
    [InlineData("   at A.B(Int32 id) in C:\\x\\F.cs:line 123")]
    [InlineData("ip 192.168.10.25 en tijd 14:05:00.12")]
    public void Filter_LaatVersiesEnFramesMet2Decimalen_Intact(string invoer)
        => Assert.Equal(invoer, FoutLogFilter.Filter(invoer));

    [Fact]
    public void Filter_VerwijdertNulTekens()
        => Assert.Equal("ab", FoutLogFilter.Filter("a\u0000b"));
}
