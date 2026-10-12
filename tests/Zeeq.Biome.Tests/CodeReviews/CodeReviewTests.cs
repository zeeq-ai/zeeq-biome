using Microsoft.Extensions.Time.Testing;
using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CodeReviews;

public sealed class CodeReviewTests
{
    [Test]
    public async Task Linear_chain_and_scenario_helper_keep_typed_relationships()
    {
        // A locked review: a named recipe traverses four levels while typed parents preserve relationships.
        // Arrange: Keep a recording store and the root builder for post-save inspection.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out var seed, store);

        // Act: Use the named locked-review recipe instead of repeating its default ancestry.
        await biome.AddLockedReview(out var activeLock).BuildAsync();

        // Assert: The leaf references its ancestors; seed rows precede the four created rows.
        await Assert
            .That(activeLock.Repository)
            .IsSameReferenceAs(activeLock.Review.PullRequest.Repository);
        await Assert.That(biome.Last<CodeReviewRecord>()).IsSameReferenceAs(activeLock.Review);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(seed.Owner);
        await Assert.That(store.Entities[1]).IsSameReferenceAs(seed.Organization);
        await Assert.That(store.Entities.Count).IsEqualTo(6);
    }

    [Test]
    public async Task Numbers_resume_across_calls_and_blueprint_variants_per_repository()
    {
        // Two repositories: sequence scope is declared on the sequence, so variants and later adds share progression.
        // Arrange: Reuse one declared sequence through an immutable closed-PR variant.
        var biome = TestBiome.Start(out _);
        var closed = Blueprints.PullRequest.With(PullRequests.Closed);

        // Act: Fan out twice, then revisit one repository for a singular PR.
        await biome
            // Create parent repositories; every Add re-anchors to its output array.
            .AddCodeRepositories(out var repos, 2)
            // Build one PR per count/customizer PER repository; explicit numbers are reserved before filling.
            .AddPullRequests(out var first, 2)
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(repos)
            // Build the requested outputs eagerly; sequence reservation covers the entire step.
            .Add(closed, out var second, count: 2)
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(repos[0])
            // Build one PR, then re-anchor to its one-element output array.
            .AddPullRequest(out var fifth)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Numbering resumes separately per repository, and dependent title/branch derives follow it.
        await Assert.That(first.Select(pr => pr.Number).ToArray()).IsEquivalentTo([1, 2, 1, 2]);
        await Assert.That(second.Select(pr => pr.Number).ToArray()).IsEquivalentTo([3, 4, 3, 4]);
        await Assert.That(fifth.Number).IsEqualTo(5);
        await Assert.That(fifth.Title).IsEqualTo("PR 5");
        await Assert.That(fifth.Branch).IsEqualTo("feature/5");
        await Assert.That(second.All(pr => pr.Closed)).IsTrue();
    }

    [Test]
    public async Task Explicit_values_are_reserved_before_unset_values_even_when_later_in_step()
    {
        // Reserved PR numbers: explicit values are skipped by the sequence before any unset output is filled.
        // Arrange: Use one repository to make all four PR customizers share a sequence scope.

        // Act: Mix unset and explicit numbers, then add another PR under the same repository.
        await TestBiome
            .Start(out _)
            // Introduce the repository parent; child sugar now becomes available.
            .AddCodeRepository(out var repo)
            // Build one PR per count/customizer PER repository; explicit numbers are reserved before filling.
            .AddPullRequests(
                out var prs,
                _ => { },
                pr => pr.Number = 1,
                pr => pr.Number = 3,
                _ => { }
            )
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(repo)
            // Build one PR, then re-anchor to its one-element output array.
            .AddPullRequest(out var next)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Reservations produce 2,1,3,4; the next call continues at 5.
        await Assert.That(prs.Select(pr => pr.Number).ToArray()).IsEquivalentTo([2, 1, 3, 4]);
        await Assert.That(prs[0].Number).IsEqualTo(2);
        await Assert.That(prs[3].Number).IsEqualTo(4);
        await Assert.That(next.Number).IsEqualTo(5);
    }

    [Test]
    public async Task Explicit_high_value_does_not_advance_sequence_and_custom_title_survives()
    {
        // An externally numbered PR: call site wins, nothing overwrites, and an explicit value does not advance progression.
        // Arrange: Model an externally supplied number/title on an otherwise default repository.

        // Act: Create the explicit PR and then a generated sibling under the same repository.
        await TestBiome
            .Start(out _)
            // Introduce the repository parent; child sugar now becomes available.
            .AddCodeRepository(out var repo)
            // Build one PR, then re-anchor to its one-element output array.
            .AddPullRequest(
                out var explicitPr,
                pr =>
                {
                    pr.Number = 99;
                    pr.Title = "Regression";
                }
            )
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(repo)
            // Build one PR, then re-anchor to its one-element output array.
            .AddPullRequest(out var generated)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Explicit 99 leaves the next generated number at 1, and the title stays customized.
        await Assert.That(explicitPr.Title).IsEqualTo("Regression");
        await Assert.That(generated.Number).IsEqualTo(1);
    }

    [Test]
    public async Task Commas_create_entities_and_combined_traits_stack_on_one_entity()
    {
        // Closed and merged PRs: commas create separate entities; + stacks traits on one entity.
        // Arrange: Use plain delegates as reusable PR traits.

        // Act: Create one closed PR and a second PR with Closed + Merged.
        await TestBiome
            .Start(out _)
            // Introduce the repository parent; child sugar now becomes available.
            .AddCodeRepository(out _)
            // Build one PR per count/customizer PER repository; explicit numbers are reserved before filling.
            .AddPullRequests(
                out var prs,
                PullRequests.Closed,
                PullRequests.Closed + PullRequests.Merged
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Two outputs exist; only the combined-trait output is merged.
        await Assert.That(prs.Length).IsEqualTo(2);
        await Assert.That(prs[0].Merged).IsFalse();
        await Assert.That(prs[1].Merged).IsTrue();
    }

    [Test]
    public async Task Transient_marks_most_recent_step_even_when_using_an_earlier_builder()
    {
        // Unsaved PR inputs: Transient targets the session’s latest Add step, regardless of the builder used.
        // Arrange: Keep the original builder and a store to compare inspection with persistence.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store);

        // Act: Add PR inputs, then mark that latest step transient through the original builder.
        biome.AddCodeRepository(out _).AddPullRequests(out var prs, 2);
        await biome.Transient().BuildAsync();

        // Assert: PRs remain inspectable but are absent from the store.
        await Assert.That(biome.All<PullRequestRecord>().Count).IsEqualTo(2);
        await Assert.That(store.Entities.OfType<PullRequestRecord>().Any()).IsFalse();
        await Assert.That(prs[0].Number).IsEqualTo(1);
    }

    [Test]
    public async Task Default_clock_is_frozen_and_precision_rounded()
    {
        // Time-stable setup: the default clock is captured once and rounded to database-friendly precision.
        // Arrange: Start a session without a supplied time provider.
        var biome = TestBiome.Start(out _);

        // Act: Build repositories before and after elapsed wall-clock time.
        biome.AddCodeRepository(out var first);
        await Task.Delay(20);
        await biome.AddCodeRepository(out var second).BuildAsync();

        // Assert: Both timestamps equal the captured start time and have microsecond precision.
        await Assert.That(second.CreatedAt).IsEqualTo(first.CreatedAt);
        await Assert.That(first.CreatedAt.Ticks % 10).IsEqualTo(0L);
    }

    [Test]
    public async Task Supplied_clock_is_live_and_now_ago_fromnow_are_rounded()
    {
        // A time-sensitive review: a shared fake provider is live, while every source of now is rounded.
        // Arrange: Give the fake clock sub-microsecond ticks to expose truncation.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero).AddTicks(17);
        var time = new FakeTimeProvider(instant);
        var biome = TestBiome.Start(out _, time: time);

        // Act: Advance the provider, then build a historical repository and future-expiring lock.
        biome.AddCodeRepository(out var first);
        time.Advance(TimeSpan.FromHours(1));
        await biome
            // Named context sugar keeps the domain call terse while exposing the rounded clock.
            .AddCodeRepository(
                out var second,
                (repo, ctx) => repo.CreatedAt = ctx.Ago(TimeSpan.FromMinutes(5))
            )
            // Build one PR, then re-anchor to its one-element output array.
            .AddPullRequest(out _)
            // Wire the PR directly and recover its repository through recorded ancestors.
            .AddCodeReview(out _)
            // Create the lock beneath its review using the session’s rounded FromNow.
            .AddActiveLock(out var activeLock)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Old values stay fixed; new Ago/FromNow values use the advanced rounded clock.
        await Assert.That(first.CreatedAt).IsEqualTo(instant.AddTicks(-7));
        await Assert.That(second.CreatedAt).IsEqualTo(instant.AddTicks(-7).AddMinutes(55));
        await Assert.That(activeLock.ExpiresAt).IsEqualTo(instant.AddTicks(-7).AddMinutes(65));
    }

    [Test]
    public async Task Child_context_exposes_parent_indices_and_accepts_base_context_traits()
    {
        // Context-aware fan-out: base-context traits also work on children; parent and child indices remain distinct.
        // Arrange: Define one trait against the base context and collect child fan-out positions.
        var positions = new List<(int Parent, int Child)>();
        Action<PullRequestRecord, BiomeContext<CodeReviewSeed>> title = (pr, ctx) =>
            pr.Title = ctx.Seed.Owner.Id;

        // Act: Apply both context-aware customizers under two repository parents.
        await TestBiome
            .Start(out _)
            // Create parent repositories; every Add re-anchors to its output array.
            .AddCodeRepositories(out var repos, 2)
            // Build the requested outputs eagerly; sequence reservation covers the entire step.
            .Add(
                Blueprints.PullRequest,
                out var prs,
                title,
                (pr, ctx) =>
                {
                    positions.Add((ctx.ParentIndex, ctx.Index));
                    pr.Title = ctx.Parent.Name;
                }
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Parent indices vary while child indices restart; the base-context trait still runs.
        await Assert.That(prs[0].Title).IsEqualTo("owner");
        await Assert.That(prs[2].Repository).IsSameReferenceAs(repos[1]);
        await Assert.That(positions.ToArray()).IsEquivalentTo([(0, 1), (1, 1)]);
    }
}
