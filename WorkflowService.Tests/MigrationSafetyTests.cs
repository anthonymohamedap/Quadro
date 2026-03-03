using System.IO;
using Xunit;

namespace WorkflowService.Tests;

public class MigrationSafetyTests
{
    [Fact]
    public void MigrationA_IsIdempotentForSettings()
    {
        var content = File.ReadAllText(Path.Combine("..", "Migrations", "20260303090000_AddStaaflijstSettingsAndFlag.cs"));
        Assert.Contains("NOT EXISTS", content);
        Assert.Contains("StaaflijstWinstFactor", content);
        Assert.Contains("StaaflijstAfvalPercentage", content);
    }

    [Fact]
    public void MigrationB_DropsAndRestoresMarginColumns()
    {
        var content = File.ReadAllText(Path.Combine("..", "Migrations", "20260303091000_RemoveTypeLijstMarginColumns.cs"));
        Assert.Contains("DropColumn", content);
        Assert.Contains("WinstMargeFactor", content);
        Assert.Contains("AfvalPercentage", content);
        Assert.Contains("precision: 6", content);
        Assert.Contains("precision: 5", content);
    }
}
