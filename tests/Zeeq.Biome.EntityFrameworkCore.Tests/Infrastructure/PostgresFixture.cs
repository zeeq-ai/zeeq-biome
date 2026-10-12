using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core;
using TUnit.Core.Interfaces;
using Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// One Postgres container for the whole test session. When Docker is unavailable, tests that call
/// <see cref="CreateContext" /> are skipped with a clear reason (but never on GitHub Actions, where Docker
/// is expected and a silent skip would hide the regression these tests guard against).
/// </summary>
public sealed class PostgresFixture : IAsyncInitializer, IAsyncDisposable
{
    private PostgreSqlContainer? container;
    private string? connectionString;
    private string? unavailableReason;

    public async Task InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await container.StartAsync();
        }
        catch (Exception ex)
            when (ex is DockerUnavailableException or HttpRequestException or SocketException
                && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            )
        {
            unavailableReason =
                $"Docker is unavailable, so Postgres tests were skipped: {ex.Message}";
            return;
        }

        connectionString = container.GetConnectionString();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>Creates a context on the shared database, skipping the calling test when Docker is unavailable.</summary>
    internal ReviewDb CreateContext()
    {
        if (unavailableReason is not null)
        {
            Skip.Test(unavailableReason);
        }
        return new(new DbContextOptionsBuilder<ReviewDb>().UseNpgsql(connectionString).Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}
