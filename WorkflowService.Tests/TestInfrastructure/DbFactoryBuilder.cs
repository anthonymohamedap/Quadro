using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using System;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public static class DbFactoryBuilder
{
    public static IDbContextFactory<AppDbContext> CreateInMemoryFactory(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PooledDbContextFactory<AppDbContext>(options);
    }

    public static async Task<SqliteTestDatabase> CreateSqliteAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var factory = new PooledDbContextFactory<AppDbContext>(options);

        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        return new SqliteTestDatabase(connection, factory);
    }
}

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    internal SqliteTestDatabase(SqliteConnection connection, IDbContextFactory<AppDbContext> factory)
    {
        _connection = connection;
        Factory = factory;
    }

    public IDbContextFactory<AppDbContext> Factory { get; }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
