using System;
using System.IO;
using System.Linq;
using QuadroApp.Service;
using Serilog.Events;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-31 — logging-configuratie.</summary>
public class LoggingSetupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuadroLogTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void CreateLogger_WritesToRollingFile()
    {
        using (var logger = LoggingSetup.CreateLogger(_dir))
        {
            logger.Information("testregel {Waarde}", 42);
        }

        var files = Directory.GetFiles(Path.Combine(_dir, "logs"), "quadro-*.log");
        var content = File.ReadAllText(Assert.Single(files));
        Assert.Contains("testregel 42", content);
        Assert.Contains("[INF]", content);
    }

    [Fact]
    public void CreateLogger_RespectsMinimumLevel()
    {
        using (var logger = LoggingSetup.CreateLogger(_dir, "warning"))
        {
            logger.Information("mag niet verschijnen");
            logger.Warning("mag wel verschijnen");
        }

        var file = Directory.GetFiles(Path.Combine(_dir, "logs"), "quadro-*.log").Single();
        var content = File.ReadAllText(file);
        Assert.DoesNotContain("mag niet verschijnen", content);
        Assert.Contains("mag wel verschijnen", content);
    }

    [Theory]
    [InlineData(null, LogEventLevel.Information)]
    [InlineData("", LogEventLevel.Information)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("WARN", LogEventLevel.Warning)]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("onzin", LogEventLevel.Information)]
    public void ParseLevel_HandlesVariants(string? input, LogEventLevel expected)
    {
        Assert.Equal(expected, LoggingSetup.ParseLevel(input));
    }
}
