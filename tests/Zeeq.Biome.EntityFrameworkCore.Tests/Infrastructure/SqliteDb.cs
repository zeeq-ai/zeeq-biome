using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;
using Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>An isolated in-memory SQLite database per test; the connection stays open for the test's lifetime.</summary>
public sealed class SqliteDb : IAsyncInitializer, IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>Creates a new context on the shared connection, e.g. a fresh one to read back what was saved.</summary>
    internal ReviewDb CreateContext() =>
        new(new DbContextOptionsBuilder<ReviewDb>().UseSqlite(connection).Options);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
