using System.Globalization;
using Avalonia.Data;
using QuadroApp.Converters;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Afgesproken prijs: leeg vak = geen afgesproken prijs (berekende prijs), 0 blijft 0.</summary>
public class LeegIsNullDecimalConverterTests
{
    private static readonly CultureInfo Nl = CultureInfo.GetCultureInfo("nl-BE");
    private static object? Terug(string? tekst)
        => LeegIsNullDecimalConverter.Instance.ConvertBack(tekst, typeof(decimal?), null, Nl);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("€ ")]
    public void Leeg_GeeftNull(string? tekst) => Assert.Null(Terug(tekst));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("45", 45)]
    [InlineData("45,5", 45.5)]
    [InlineData("45.5", 45.5)]
    [InlineData("€ 45,50", 45.5)]
    [InlineData("1.234,50", 1234.5)]
    public void Getal_WordtGelezen(string tekst, double verwacht) => Assert.Equal((decimal)verwacht, Terug(tekst));

    [Fact]
    public void GeenGetal_GeeftFout() => Assert.IsType<BindingNotification>(Terug("abc"));

    [Fact]
    public void Null_ToontLeegVak()
        => Assert.Equal("", LeegIsNullDecimalConverter.Instance.Convert(null, typeof(string), null, Nl));
}
