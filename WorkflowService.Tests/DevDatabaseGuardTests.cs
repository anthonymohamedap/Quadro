using QuadroApp.Service.Security;
using System.IO;
using Xunit;

namespace WorkflowService.Tests;

public class DevDatabaseGuardTests
{
    private const string Cloud = "Host=212.47.241.9;Port=20900;Database=quadrodb;Username=quadro;Password=x;SSL Mode=Require";
    private static readonly string DataDir = Path.Combine(Path.GetTempPath(), "QuadroAppTest");

    [Fact]
    public void Debug_MetPostgres_WordtOmgeleidNaarDevSqlite()
    {
        var cs = DevDatabaseGuard.Apply(Cloud, isDebugBuild: true, allowPgValue: null, DataDir, out var redirected);

        Assert.True(redirected);
        Assert.Equal($"Data Source={Path.Combine(DataDir, DevDatabaseGuard.DevDbFileName)}", cs);
        Assert.DoesNotContain("212.47.241.9", cs);
    }

    [Fact]
    public void Debug_MetExplicieteToestemming_BlijftOpPostgres()
    {
        var cs = DevDatabaseGuard.Apply(Cloud, isDebugBuild: true, allowPgValue: "1", DataDir, out var redirected);

        Assert.False(redirected);
        Assert.Equal(Cloud, cs);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("")]
    public void Debug_AndereWaardeDan1_GeeftGeenToestemming(string waarde)
    {
        DevDatabaseGuard.Apply(Cloud, isDebugBuild: true, allowPgValue: waarde, DataDir, out var redirected);
        Assert.True(redirected);
    }

    [Fact]
    public void Release_WordtNooitAangepast()
    {
        var cs = DevDatabaseGuard.Apply(Cloud, isDebugBuild: false, allowPgValue: null, DataDir, out var redirected);

        Assert.False(redirected);
        Assert.Equal(Cloud, cs);
    }

    [Fact]
    public void Debug_MetSqlite_BlijftOngewijzigd()
    {
        const string sqlite = "Data Source=quadro.db";
        var cs = DevDatabaseGuard.Apply(sqlite, isDebugBuild: true, allowPgValue: null, DataDir, out var redirected);

        Assert.False(redirected);
        Assert.Equal(sqlite, cs);
    }
}
