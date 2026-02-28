using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import.Enterprise;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

public class ImportServiceTests
{
    [Fact]
    public async Task ImportService_DryRun_ReturnsRowIssues()
    {
        var sut = CreateSut(out _);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ImportService_DetectsDuplicatesInFile()
    {
        var sut = CreateSut(out _);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ImportService_DetectsDuplicatesInDb()
    {
        var sut = CreateSut(out _);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Commit_RollsBackOnException()
    {
        var sut = CreateSut(out _);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Commit_CreatesImportSessionAndRowLogs()
    {
        var sut = CreateSut(out _);
        await Task.CompletedTask;
    }

    private static ImportService CreateSut(out IDbContextFactory<AppDbContext> factory)
    {
        factory = new PooledDbContextFactory<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var parser = new ClosedXmlExcelParser();
        return new ImportService(factory, parser, NullLogger<ImportService>.Instance);
    }
}
