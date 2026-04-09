using System;
using System.IO;
using Xunit;

namespace WorkflowService.Tests;

public class MigrationSafetyTests
{
    private static string GetMigrationPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Migrations", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Migration file '{fileName}' was not found.");
    }

    [Fact]
    public void MigrationA_InitialClean_BevatKernTabellen()
    {
        var content = File.ReadAllText(GetMigrationPath("20260228232856_InitialClean.cs"));
        Assert.Contains("TypeLijsten", content);
        Assert.Contains("Leveranciers", content);
        Assert.Contains("Offertes", content);
    }

    [Fact]
    public void MigrationB_NullableLeverancierIdOnTypeLijst_AlterColumn()
    {
        var content = File.ReadAllText(GetMigrationPath("20260407000000_NullableLeverancierIdOnTypeLijst.cs"));
        Assert.Contains("AlterColumn", content);
        Assert.Contains("LeverancierId", content);
        Assert.Contains("TypeLijsten", content);
        Assert.Contains("nullable: true", content);
    }
}
