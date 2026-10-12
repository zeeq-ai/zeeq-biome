using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.ProjectManagement;

public sealed class ProjectManagementTests
{
    [Test]
    public async Task Portfolio_tree_and_comment_siblings_build_six_levels_with_many_leaves()
    {
        // A multi-workspace portfolio: six levels, three rank scopes, and sibling comments.
        // Arrange: a recording store makes the creation order visible independently of inspection.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store);

        // Act: each child count multiplies the previous output array.
        await biome
            .AddWorkspaces(out var workspaces, 2)
            // 2 × 2 = 4 projects; a board per project keeps the next layer readable.
            .AddProjects(out _, 2)
            .AddBoards(out _, 1)
            // 4 boards × 2 columns = 8 columns.
            .AddColumns(out var columns, 2)
            // 8 columns × 3 cards = 24 cards.
            .AddCards(out var cards, 3)
            // 24 cards × 4 checklist entries = 96 deepest leaves.
            .AddChecklistItems(out var checklist, 4)
            // Comments attach to cards, so jump back from checklist leaves to the saved card output.
            .Under(cards)
            .AddComments(out var comments, 2)
            .BuildAsync();

        // Assert: frozen time makes all sibling timestamps identical; ancestor lookup preserves tenancy.
        await Assert.That(biome.All<Workspace>().Count).IsEqualTo(2);
        await Assert.That(biome.All<Project>().Count).IsEqualTo(2 * 2);
        await Assert.That(biome.All<Board>().Count).IsEqualTo(2 * 2 * 1);
        await Assert.That(biome.All<Column>().Count).IsEqualTo(2 * 2 * 1 * 2);
        await Assert.That(biome.All<Card>().Count).IsEqualTo(2 * 2 * 1 * 2 * 3);
        await Assert.That(biome.All<ChecklistItem>().Count).IsEqualTo(2 * 2 * 1 * 2 * 3 * 4);
        await Assert.That(biome.All<Comment>().Count).IsEqualTo(24 * 2);
        await Assert.That(store.Entities.Count).IsEqualTo(2 + 4 + 4 + 8 + 24 + 96 + 48);
        await Assert.That(checklist[95].Workspace).IsSameReferenceAs(workspaces[1]);
        await Assert.That(comments[47].Card).IsSameReferenceAs(cards[23]);
        await Assert.That(columns[2].Rank).IsEqualTo("a");
        await Assert.That(cards[3].Rank).IsEqualTo("a");
        await Assert.That(checklist[4].Rank).IsEqualTo("a");
        await Assert.That(comments[47].CreatedAt).IsEqualTo(comments[0].CreatedAt);
        await Assert.That(cards[0].DueAt - comments[0].CreatedAt).IsEqualTo(TimeSpan.FromHours(25));
    }

    [Test]
    public async Task Local_ranks_skip_explicit_values_while_tickets_span_sibling_columns()
    {
        // Two columns share a workspace ticket sequence but have independent local rank sequences.
        // Arrange: the sequence scope is declared in Domain.cs, not chosen by Under or a customizer.
        var biome = TestBiome.Start(out _);

        // Act: each customizer runs once per column; + stacks card traits on one output.
        await biome
            .AddWorkspace(out _)
            .AddProject(out _)
            .AddBoard(out _)
            .AddColumns(out var columns, 2)
            .AddCards(
                out var cards,
                Cards.Blocked + Cards.Urgent,
                // Explicit "a" is reserved before the first card is filled; its rank becomes "aa".
                c =>
                {
                    c.Rank = "a";
                    c.Title = "Regression";
                }
            )
            .AddChecklistItems(out _, 2)
            // Resume one column's rank and the encompassing workspace's ticket sequence.
            .Under(columns[0])
            .AddCard(out var next)
            .BuildAsync();

        // Assert: call site wins, nothing overwrites the explicit title.
        await Assert.That(cards[0].Blocked && cards[0].Urgent).IsTrue();
        await Assert.That(cards[0].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Ticket).IsEqualTo(3);
        await Assert.That(cards[1].Title).IsEqualTo("Regression");
        await Assert.That(next.Rank).IsEqualTo("aaa");
        await Assert.That(next.Ticket).IsEqualTo(5);
        await Assert.That(next.Title).IsEqualTo("Ticket 5");
        await Assert.That(biome.All<ChecklistItem>().Count).IsEqualTo(2 * 2 * 2);
    }

    [Test]
    public async Task Ready_checklist_helper_and_transient_comment_use_live_time()
    {
        // A default six-level branch plus a preview-only comment after a fake-clock advance.
        // Arrange: retain the store and root builder to compare persisted and created entities.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store, time);
        biome.AddReadyChecklist(out var leaf);

        // Act: Under(card) selects the comment sibling branch, not a child of the checklist item.
        time.Advance(TimeSpan.FromHours(2));
        await biome
            .Under(leaf.Card)
            .AddComment(out var preview)
            // Built and returned, but NOT saved: this models a draft comment request.
            .Transient()
            .BuildAsync();

        // Assert: the helper returned its leaf and left all six graph levels inspectable.
        await Assert.That(biome.All<ChecklistItem>().Count).IsEqualTo(1);
        await Assert.That(biome.All<Comment>().Count).IsEqualTo(1);
        await Assert.That(store.Entities.Count).IsEqualTo(6);
        await Assert.That(store.Entities.OfType<Comment>().Any()).IsFalse();
        await Assert.That(preview.CreatedAt).IsEqualTo(time.GetUtcNow().AddHours(-1));
        await Assert.That(leaf.Workspace).IsSameReferenceAs(biome.Last<Workspace>());
    }
}
