namespace Zeeq.Biome.Tests.Board;

public sealed class BoardTests
{
    [Test]
    public async Task Ranks_progress_per_parent_and_under_resumes_sibling_branches()
    {
        // Board siblings: PerParent ranks resume under old columns and restart under new ones.
        // Arrange: Keep earlier board/column outputs for sibling navigation.

        // Act: Build card fan-out, revisit a column, then create a new sibling column.
        await TestBiome
            .Start(out _)
            // Create the board parent before ordering its children.
            .AddBoard(out var board)
            // Create sibling columns; each owns a separate parent-scoped card sequence.
            .AddColumns(out var columns, 2)
            // Build cards PER column; rank scope is the parent identity, not its array position.
            .AddCards(out var cards, 2)
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(columns[0])
            // Continue only this column’s rank sequence.
            .AddCard(out var next)
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(board)
            // Create a new sibling column with a fresh card rank scope.
            .AddColumn(out var sibling)
            // Continue only this column’s rank sequence.
            .AddCard(out var siblingCard)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Old columns resume ranks, new columns restart them, and parent references remain correct.
        await Assert.That(cards[0].Rank).IsEqualTo("a");
        await Assert.That(cards[1].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Rank).IsEqualTo("a");
        await Assert.That(cards[3].Rank).IsEqualTo("aa");
        await Assert.That(next.Rank).IsEqualTo("aaa");
        await Assert.That(siblingCard.Rank).IsEqualTo("a");
        await Assert.That(sibling.Board).IsSameReferenceAs(board);
    }

    [Test]
    public async Task Explicit_rank_is_reserved_only_in_its_parent_scope()
    {
        // Local rank reservations: one column’s explicit rank cannot consume another column’s sequence.
        // Arrange: Create two columns with independently scoped rank progression.

        // Act: Reserve a rank under the first column, then add a card under the second.
        await TestBiome
            .Start(out _)
            // Create the board parent before ordering its children.
            .AddBoard(out _)
            // Create sibling columns; each owns a separate parent-scoped card sequence.
            .AddColumns(out var columns, 2)
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(columns[0])
            // Build cards PER column; rank scope is the parent identity, not its array position.
            .AddCards(out var first, _ => { }, c => c.Rank = "a")
            // Jump back to a saved parent/output; every Add had re-anchored to its own result.
            .Under(columns[1])
            // Continue only this column’s rank sequence.
            .AddCard(out var other)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: The first skips to aa while the second still begins at a.
        await Assert.That(first[0].Rank).IsEqualTo("aa");
        await Assert.That(other.Rank).IsEqualTo("a");
    }
}
