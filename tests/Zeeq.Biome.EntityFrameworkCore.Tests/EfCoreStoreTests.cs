using Microsoft.EntityFrameworkCore;
using Zeeq.Biome.EntityFrameworkCore.Tests.Infrastructure;
using Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

namespace Zeeq.Biome.EntityFrameworkCore.Tests;

[ClassDataSource<SqliteDb>]
public sealed class EfCoreStoreTests(SqliteDb sqlite)
{
    [Test]
    public async Task Seed_and_chain_are_saved_with_generated_keys_and_fixed_up_foreign_keys()
    {
        await using var db = sqlite.CreateContext();
        await TestBiome
            .Start(db, out var seed)
            .AddCodeRepository(out var repo)
            .AddPullRequests(out var prs, 2)
            .AddCodeReviews(out var reviews, 1)
            .BuildAsync();

        await using var read = sqlite.CreateContext();
        var savedRepo = await read
            .Repositories.Include(r => r.Organization)
            .SingleAsync(r => r.Id == repo.Id);
        var savedPrs = await read
            .PullRequests.Where(pr => pr.Repository.Id == repo.Id)
            .OrderBy(pr => pr.Number)
            .ToListAsync();
        var savedReviews = await read
            .Reviews.Include(r => r.PullRequest)
            .OrderBy(r => r.Id)
            .ToListAsync();

        await Assert.That(repo.Id).IsGreaterThan(0);
        await Assert.That(savedRepo.Organization.Id).IsEqualTo(seed.Organization.Id);
        await Assert.That((await read.Users.SingleAsync()).Id).IsEqualTo(seed.Owner.Id);
        await Assert
            .That(savedPrs.Select(pr => pr.Id).ToArray())
            .IsEquivalentTo(prs.Select(pr => pr.Id).ToArray());
        await Assert
            .That(savedPrs.Select(pr => pr.Title!).ToArray())
            .IsEquivalentTo(["PR 1", "PR 2"]);
        await Assert
            .That(savedReviews.Select(r => r.Id).ToArray())
            .IsEquivalentTo(reviews.Select(r => r.Id).ToArray());
        await Assert
            .That(savedReviews.Select(r => r.PullRequest.Id).ToArray())
            .IsEquivalentTo(prs.Select(pr => pr.Id).ToArray());
    }

    [Test]
    public async Task Transient_entities_are_not_saved()
    {
        await using var db = sqlite.CreateContext();
        await TestBiome
            .Start(db, out _)
            .AddCodeRepository(out var repo)
            .AddPullRequest(out var kept)
            .Under(repo)
            .AddPullRequest(out var draft)
            .Transient()
            .BuildAsync();

        await using var read = sqlite.CreateContext();
        await Assert.That(draft.Id).IsEqualTo(0);
        await Assert.That((await read.PullRequests.SingleAsync()).Id).IsEqualTo(kept.Id);
        await Assert.That(await read.ReviewQueue.CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Aggregates_are_expanded_into_their_rows()
    {
        await using var db = sqlite.CreateContext();
        await TestBiome
            .Start(db, out var seed)
            .AddOrganization(out var graph, input => input.IsDefault = true)
            .BuildAsync();

        await using var read = sqlite.CreateContext();
        var membership = await read
            .Memberships.Include(m => m.User)
            .Include(m => m.Organization)
            .SingleAsync();
        await Assert.That(membership.Organization.Id).IsEqualTo(graph.Organization.Id);
        await Assert.That(membership.User.Id).IsEqualTo(seed.Owner.Id);
        await Assert.That(membership.IsDefault).IsTrue();
        await Assert.That(await read.Organizations.CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task Rows_added_by_before_save_hooks_are_saved()
    {
        await using var db = sqlite.CreateContext();
        await TestBiome
            .Start(db, out _)
            .AddCodeRepository(out _)
            .AddPullRequests(out var prs, _ => { }, PullRequests.Closed)
            .BuildAsync();

        await using var read = sqlite.CreateContext();
        var queued = await read.ReviewQueue.Include(q => q.PullRequest).SingleAsync();
        await Assert.That(queued.PullRequest.Id).IsEqualTo(prs[0].Id);
    }

    [Test]
    public async Task Cancellation_is_honored_and_nothing_is_saved()
    {
        await using var db = sqlite.CreateContext();
        var org = new Organization { Id = "org_cancelled" };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert
            .That(async () => await db.AsBiomeStore().SaveAsync([org], cts.Token))
            .Throws<OperationCanceledException>();

        await using var read = sqlite.CreateContext();
        await Assert.That(await read.Organizations.CountAsync()).IsEqualTo(0);
    }
}
