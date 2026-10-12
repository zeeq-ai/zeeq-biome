using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Zeeq.Biome.EntityFrameworkCore.Tests.Infrastructure;
using Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

namespace Zeeq.Biome.EntityFrameworkCore.Tests;

/// <summary>
/// Postgres <c>timestamptz</c> stores microseconds; .NET stores 100ns ticks. Biome truncates every clock reading to
/// 1µs by default so a value written by a scenario is exactly the value read back.
/// </summary>
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public sealed class PostgresTimeTests(PostgresFixture postgres)
{
    // Seven fractional digits: the last tick is below Postgres precision.
    private static readonly DateTimeOffset SubMicrosecond = new DateTimeOffset(
        2026,
        10,
        11,
        12,
        0,
        0,
        TimeSpan.Zero
    ).AddTicks(1_234_567);

    [Test]
    public async Task Now_and_Ago_round_trip_through_timestamptz_exactly()
    {
        await using var db = postgres.CreateContext();
        var time = new FakeTimeProvider(SubMicrosecond);
        await TestBiome
            .Start(db, out _, time)
            .AddCodeRepository(out var repo)
            .AddPullRequest(out var pr)
            .AddCodeReview(out var review)
            .BuildAsync();

        await using var read = postgres.CreateContext();
        var savedRepo = await read.Repositories.SingleAsync(r => r.Id == repo.Id);
        var savedReview = await read.Reviews.SingleAsync(r => r.Id == review.Id);
        await Assert.That(savedRepo.CreatedAt).IsEqualTo(repo.CreatedAt);
        await Assert.That(savedRepo.CreatedAt).IsEqualTo(SubMicrosecond.AddTicks(-7));
        await Assert.That(savedReview.CreatedAt).IsEqualTo(review.CreatedAt);
        await Assert
            .That(savedReview.CreatedAt)
            .IsEqualTo(SubMicrosecond.AddTicks(-7).AddMinutes(-5));
    }

    [Test]
    public async Task The_frozen_default_clock_round_trips_exactly()
    {
        await using var db = postgres.CreateContext();
        await TestBiome.Start(db, out _).AddCodeRepository(out var repo).BuildAsync();

        await using var read = postgres.CreateContext();
        await Assert
            .That((await read.Repositories.SingleAsync(r => r.Id == repo.Id)).CreatedAt)
            .IsEqualTo(repo.CreatedAt);
    }

    [Test]
    public async Task A_clock_advanced_between_adds_still_round_trips_exactly()
    {
        await using var db = postgres.CreateContext();
        var time = new FakeTimeProvider(SubMicrosecond);
        var biome = TestBiome.Start(db, out _, time);
        biome.AddCodeRepository(out var first);
        time.Advance(TimeSpan.FromTicks(15));
        biome.AddCodeRepository(out var second);
        await biome.BuildAsync();

        await using var read = postgres.CreateContext();
        var saved = await read
            .Repositories.Where(r => r.Id == first.Id || r.Id == second.Id)
            .OrderBy(r => r.Id)
            .ToListAsync();
        await Assert
            .That(saved.Select(r => r.CreatedAt).ToArray())
            .IsEquivalentTo([first.CreatedAt, second.CreatedAt]);
    }

    [Test]
    public async Task Control_an_unrounded_value_does_not_round_trip()
    {
        await using var db = postgres.CreateContext();
        var repo = new CodeRepository
        {
            Organization = new() { Id = $"org_{Guid.CreateVersion7():N}" },
            CreatedAt = SubMicrosecond,
        };
        db.Add(repo);
        await db.SaveChangesAsync();

        await using var read = postgres.CreateContext();
        var saved = await read.Repositories.SingleAsync(r => r.Id == repo.Id);
        await Assert.That(saved.CreatedAt).IsNotEqualTo(SubMicrosecond);
    }
}
