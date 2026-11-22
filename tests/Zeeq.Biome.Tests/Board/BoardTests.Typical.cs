namespace Zeeq.Biome.Tests.Board;

// The same scenarios as BoardTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class BoardTests
{
    [Test]
    public async Task Ranks_progress_per_parent_and_under_resumes_sibling_branches_typical()
    {
        // Arrange: parents, parent links and every rank string are written out by hand.
        var board = new Board();
        var columns = new[]
        {
            new Column { Board = board },
            new Column { Board = board },
        };

        // Each column restarts its own rank sequence: a, aa.
        string[] ranks = ["a", "aa"];
        var cards = columns
            .SelectMany(c => ranks.Select(rank => new Card { Column = c, Rank = rank }))
            .ToArray();

        // Resume only the first column's progression by remembering its last rank.
        var next = new Card { Column = columns[0], Rank = "aaa" };

        // A new column restarts at "a".
        var sibling = new Column { Board = board };
        var siblingCard = new Card { Column = sibling, Rank = "a" };

        // Assert: identical to the Biome version.
        await Assert.That(cards[0].Rank).IsEqualTo("a");
        await Assert.That(cards[1].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Rank).IsEqualTo("a");
        await Assert.That(cards[3].Rank).IsEqualTo("aa");
        await Assert.That(next.Rank).IsEqualTo("aaa");
        await Assert.That(siblingCard.Rank).IsEqualTo("a");
        await Assert.That(sibling.Board).IsSameReferenceAs(board);
    }

    [Test]
    public async Task Explicit_rank_is_reserved_only_in_its_parent_scope_typical()
    {
        // Arrange: reserving "a" by hand means the other card in that column must use "aa".
        var board = new Board();
        var columns = new[]
        {
            new Column { Board = board },
            new Column { Board = board },
        };
        var first = new[]
        {
            new Card { Column = columns[0], Rank = "aa" },
            new Card { Column = columns[0], Rank = "a" },
        };
        // The other column's sequence is untouched.
        var other = new Card { Column = columns[1], Rank = "a" };

        // Assert: identical to the Biome version.
        await Assert.That(first[0].Rank).IsEqualTo("aa");
        await Assert.That(other.Rank).IsEqualTo("a");
    }
}
