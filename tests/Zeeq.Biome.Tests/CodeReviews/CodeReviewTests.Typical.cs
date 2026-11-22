namespace Zeeq.Biome.Tests.CodeReviews;

// The same scenarios as CodeReviewTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
// Not repeated here: clock rounding/freezing and context-index tests, which exercise Biome itself.
public sealed partial class CodeReviewTests
{
    private static CodeRepository NewRepository(Organization org, int n = 1) =>
        new()
        {
            Id = $"repo-{n}",
            Organization = org,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    // Number, title and branch must stay consistent, so every PR repeats all three.
    private static PullRequestRecord NewPullRequest(CodeRepository repo, int number) =>
        new()
        {
            Id = $"pr-{repo.Id}-{number}",
            Repository = repo,
            Number = number,
            Title = $"PR {number}",
            Branch = $"feature/{number}",
        };

    [Test]
    public async Task Linear_chain_and_scenario_helper_keep_typed_relationships_typical()
    {
        // Arrange: seed rows, then the whole four-level ancestry, wired by hand.
        var owner = new User();
        var org = new Organization();
        var repo = NewRepository(org);
        var pr = NewPullRequest(repo, 1);
        var review = new CodeReviewRecord
        {
            Id = "review-1",
            PullRequest = pr,
            Repository = repo,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var activeLock = new ActiveCodeReviewLock
        {
            Review = review,
            Repository = repo,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        };
        var saved = new List<object> { owner, org, repo, pr, review, activeLock };

        // Assert: identical to the Biome version.
        await Assert
            .That(activeLock.Repository)
            .IsSameReferenceAs(activeLock.Review.PullRequest.Repository);
        await Assert
            .That(saved.OfType<CodeReviewRecord>().Last())
            .IsSameReferenceAs(activeLock.Review);
        await Assert.That(saved[0]).IsSameReferenceAs(owner);
        await Assert.That(saved[1]).IsSameReferenceAs(org);
        await Assert.That(saved.Count).IsEqualTo(6);
    }

    [Test]
    public async Task Numbers_resume_across_calls_and_blueprint_variants_per_repository_typical()
    {
        // Arrange: PR numbers are tracked by hand for each repository.
        var org = new Organization();
        var repos = new[] { NewRepository(org, 1), NewRepository(org, 2) };
        var first = repos
            .SelectMany(r => new[] { NewPullRequest(r, 1), NewPullRequest(r, 2) })
            .ToArray();
        var second = repos
            .SelectMany(r => new[] { NewPullRequest(r, 3), NewPullRequest(r, 4) })
            .ToArray();
        foreach (var pr in second)
            pr.Closed = true;
        var fifth = NewPullRequest(repos[0], 5);

        // Assert: identical to the Biome version.
        await Assert.That(first.Select(pr => pr.Number).ToArray()).IsEquivalentTo([1, 2, 1, 2]);
        await Assert.That(second.Select(pr => pr.Number).ToArray()).IsEquivalentTo([3, 4, 3, 4]);
        await Assert.That(fifth.Number).IsEqualTo(5);
        await Assert.That(fifth.Title).IsEqualTo("PR 5");
        await Assert.That(fifth.Branch).IsEqualTo("feature/5");
        await Assert.That(second.All(pr => pr.Closed)).IsTrue();
    }

    [Test]
    public async Task Explicit_values_are_reserved_before_unset_values_even_when_later_in_step_typical()
    {
        // Arrange: reserve 1 and 3 by hand, so the unset PRs take 2 and 4.
        var repo = NewRepository(new Organization());
        var prs = new[]
        {
            NewPullRequest(repo, 2),
            NewPullRequest(repo, 1),
            NewPullRequest(repo, 3),
            NewPullRequest(repo, 4),
        };
        var next = NewPullRequest(repo, 5);

        // Assert: identical to the Biome version.
        await Assert.That(prs.Select(pr => pr.Number).ToArray()).IsEquivalentTo([2, 1, 3, 4]);
        await Assert.That(prs[0].Number).IsEqualTo(2);
        await Assert.That(prs[3].Number).IsEqualTo(4);
        await Assert.That(next.Number).IsEqualTo(5);
    }

    [Test]
    public async Task Explicit_high_value_does_not_advance_sequence_and_custom_title_survives_typical()
    {
        // Arrange: an external number does not affect the generated sibling.
        var repo = NewRepository(new Organization());
        var explicitPr = NewPullRequest(repo, 99);
        explicitPr.Title = "Regression";
        var generated = NewPullRequest(repo, 1);

        // Assert: identical to the Biome version.
        await Assert.That(explicitPr.Title).IsEqualTo("Regression");
        await Assert.That(generated.Number).IsEqualTo(1);
    }

    [Test]
    public async Task Commas_create_entities_and_combined_traits_stack_on_one_entity_typical()
    {
        // Arrange: two PRs; the second combines closed and merged.
        var repo = NewRepository(new Organization());
        var prs = new[] { NewPullRequest(repo, 1), NewPullRequest(repo, 2) };
        prs[0].Closed = true;
        prs[1].Closed = true;
        prs[1].Merged = true;

        // Assert: identical to the Biome version.
        await Assert.That(prs.Length).IsEqualTo(2);
        await Assert.That(prs[0].Merged).IsFalse();
        await Assert.That(prs[1].Merged).IsTrue();
    }

    [Test]
    public async Task Transient_marks_most_recent_step_even_when_using_an_earlier_builder_typical()
    {
        // Arrange: the PRs exist in memory but are never added to the saved list.
        var repo = NewRepository(new Organization());
        var prs = new[] { NewPullRequest(repo, 1), NewPullRequest(repo, 2) };
        var saved = new List<object> { repo };

        // Assert: identical to the Biome version.
        await Assert.That(prs.Length).IsEqualTo(2);
        await Assert.That(saved.OfType<PullRequestRecord>().Any()).IsFalse();
        await Assert.That(prs[0].Number).IsEqualTo(1);
    }
}
