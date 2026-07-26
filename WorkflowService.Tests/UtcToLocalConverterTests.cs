using System;
using System.Globalization;
using QuadroApp.Converters;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>REL-01 — UTC-tijdstip correct naar lokale tijd tonen.</summary>
public class UtcToLocalConverterTests
{
    private static readonly UtcToLocalConverter Sut = UtcToLocalConverter.Instance;

    [Fact]
    public void Convert_UtcDate_ShowsLocalTime()
    {
        var utc = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);
        var expectedLocal = utc.ToLocalTime().ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var result = (string?)Sut.Convert(utc, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expectedLocal, result);
    }

    [Fact]
    public void Convert_UnspecifiedKind_TreatedAsUtc()
    {
        var stored = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Unspecified);
        var expected = DateTime.SpecifyKind(stored, DateTimeKind.Utc).ToLocalTime()
            .ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var result = (string?)Sut.Convert(stored, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    public void Convert_Null_ReturnsEmpty(object? value)
    {
        Assert.Equal(string.Empty, Sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_DefaultDate_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Sut.Convert(default(DateTime), typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_CustomFormat_IsUsed()
    {
        var utc = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);
        var result = (string?)Sut.Convert(utc, typeof(string), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(utc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), result);
    }
}
