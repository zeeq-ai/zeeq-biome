using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.ProjectManagement;

// The same scenarios as ProjectManagementTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class ProjectManagementTests
{
    // Ranks grow a, aa, aaa...; each parent restarts its own run.
    private static string Rank(int index) => new('a', index + 1);

    [Test]
    public async Task Portfolio_tree_and_comment_siblings_build_six_levels_with_many_leaves_typical()
    {
        // Arrange: six nested loops with three rank counters and a per-workspace ticket counter.
        var now = DateTimeOffset.UtcNow;
        var workspaces = new List<Workspace>();
        var projects = new List<Project>();
        var boards = new List<Board>();
        var columns = new List<Column>();
        var cards = new List<Card>();
        var checklist = new List<ChecklistItem>();

        for (var w = 0; w < 2; w++)
        {
            var workspace = new Workspace { Owner = "owner" };
            workspaces.Add(workspace);
            var ticket = 1; // per workspace, across projects, boards and columns
            for (var p = 0; p < 2; p++)
            {
                var project = new Project { Workspace = workspace };
                projects.Add(project);
                var board = new Board { Project = project };
                boards.Add(board);
                for (var c = 0; c < 2; c++)
                {
                    var column = new Column
                    {
                        Board = board,
                        Rank = Rank(c),
                        Name = $"Column {Rank(c)}",
                    };
                    columns.Add(column);
                    for (var k = 0; k < 3; k++)
                    {
                        var card = new Card
                        {
                            Column = column,
                            Workspace = workspace,
                            Rank = Rank(k),
                            Ticket = ticket,
                            Title = $"Ticket {ticket++}",
                            DueAt = now.AddDays(1),
                        };
                        cards.Add(card);
                        for (var i = 0; i < 4; i++)
                        {
                            checklist.Add(
                                new ChecklistItem
                                {
                                    Card = card,
                                    Workspace = workspace,
                                    Rank = Rank(i),
                                    Text = $"Task {Rank(i)}",
                                }
                            );
                        }
                    }
                }
            }
        }

        // Comments are siblings of the checklist items, so they get their own pass over the cards.
        var comments = cards
            .SelectMany(card =>
                Enumerable
                    .Range(1, 2)
                    .Select(n => new Comment
                    {
                        Card = card,
                        Workspace = card.Workspace,
                        Number = n,
                        Text = $"Comment {n}",
                        CreatedAt = now.AddHours(-1),
                    })
            )
            .ToList();

        var saved = new List<object>();
        saved.AddRange(workspaces);
        saved.AddRange(projects);
        saved.AddRange(boards);
        saved.AddRange(columns);
        saved.AddRange(cards);
        saved.AddRange(checklist);
        saved.AddRange(comments);

        // Assert: identical to the Biome version.
        await Assert.That(workspaces.Count).IsEqualTo(2);
        await Assert.That(projects.Count).IsEqualTo(2 * 2);
        await Assert.That(boards.Count).IsEqualTo(2 * 2 * 1);
        await Assert.That(columns.Count).IsEqualTo(2 * 2 * 1 * 2);
        await Assert.That(cards.Count).IsEqualTo(2 * 2 * 1 * 2 * 3);
        await Assert.That(checklist.Count).IsEqualTo(2 * 2 * 1 * 2 * 3 * 4);
        await Assert.That(comments.Count).IsEqualTo(24 * 2);
        await Assert.That(saved.Count).IsEqualTo(2 + 4 + 4 + 8 + 24 + 96 + 48);
        await Assert.That(checklist[95].Workspace).IsSameReferenceAs(workspaces[1]);
        await Assert.That(comments[47].Card).IsSameReferenceAs(cards[23]);
        await Assert.That(columns[2].Rank).IsEqualTo("a");
        await Assert.That(cards[3].Rank).IsEqualTo("a");
        await Assert.That(checklist[4].Rank).IsEqualTo("a");
        await Assert.That(comments[47].CreatedAt).IsEqualTo(comments[0].CreatedAt);
        await Assert.That(cards[0].DueAt - comments[0].CreatedAt).IsEqualTo(TimeSpan.FromHours(25));
    }

    [Test]
    public async Task Local_ranks_skip_explicit_values_while_tickets_span_sibling_columns_typical()
    {
        // Arrange: reserve rank "a" by hand; tickets are numbered 1-4 in creation order.
        var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace { Owner = "owner" };
        var board = new Board { Project = new Project { Workspace = workspace } };
        var columns = new[]
        {
            new Column
            {
                Board = board,
                Rank = "a",
                Name = "Column a",
            },
            new Column
            {
                Board = board,
                Rank = "aa",
                Name = "Column aa",
            },
        };
        Card NewCard(Column column, string rank, int ticket, string? title = null) =>
            new()
            {
                Column = column,
                Workspace = workspace,
                Rank = rank,
                Ticket = ticket,
                Title = title ?? $"Ticket {ticket}",
                DueAt = now.AddDays(1),
            };

        // Per column: a blocked urgent card, then the explicit "a" card; tickets run 1-4.
        var cards = new[]
        {
            NewCard(columns[0], "aa", 1),
            NewCard(columns[0], "a", 2, "Regression"),
            NewCard(columns[1], "aa", 3),
            NewCard(columns[1], "a", 4, "Regression"),
        };
        foreach (var card in new[] { cards[0], cards[2] })
        {
            card.Blocked = true;
            card.Urgent = true;
        }

        string[] ranks = ["a", "aa"];
        var checklist = cards
            .SelectMany(card =>
                ranks.Select(rank => new ChecklistItem
                {
                    Card = card,
                    Workspace = workspace,
                    Rank = rank,
                    Text = $"Task {rank}",
                })
            )
            .ToArray();
        // Resume column 0's ranks ("aa", "a" used) and the workspace tickets (1-4 used).
        var next = NewCard(columns[0], "aaa", 5);

        // Assert: identical to the Biome version.
        await Assert.That(cards[0].Blocked && cards[0].Urgent).IsTrue();
        await Assert.That(cards[0].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Rank).IsEqualTo("aa");
        await Assert.That(cards[2].Ticket).IsEqualTo(3);
        await Assert.That(cards[1].Title).IsEqualTo("Regression");
        await Assert.That(next.Rank).IsEqualTo("aaa");
        await Assert.That(next.Ticket).IsEqualTo(5);
        await Assert.That(next.Title).IsEqualTo("Ticket 5");
        await Assert.That(checklist.Length).IsEqualTo(2 * 2 * 2);
    }

    [Test]
    public async Task Ready_checklist_helper_and_transient_comment_use_live_time_typical()
    {
        // Arrange: no helper, so the six-level branch is written out in the test.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var workspace = new Workspace { Owner = "owner" };
        var board = new Board { Project = new Project { Workspace = workspace } };
        var column = new Column
        {
            Board = board,
            Rank = "a",
            Name = "Column a",
        };
        var card = new Card
        {
            Column = column,
            Workspace = workspace,
            Rank = "a",
            Ticket = 1,
            Title = "Ticket 1",
            DueAt = time.GetUtcNow().AddDays(1),
        };
        var leaf = new ChecklistItem
        {
            Card = card,
            Workspace = workspace,
            Rank = "a",
            Text = "Task a",
        };
        var saved = new List<object> { workspace, board.Project, board, column, card, leaf };

        // Act: advance the clock, then build the draft comment. It is never added to saved.
        time.Advance(TimeSpan.FromHours(2));
        var preview = new Comment
        {
            Card = card,
            Workspace = workspace,
            Number = 1,
            Text = "Comment 1",
            CreatedAt = time.GetUtcNow().AddHours(-1),
        };

        // Assert: identical to the Biome version.
        await Assert.That(preview.Card).IsSameReferenceAs(leaf.Card);
        await Assert.That(saved.Count).IsEqualTo(6);
        await Assert.That(saved.OfType<Comment>().Any()).IsFalse();
        await Assert.That(preview.CreatedAt).IsEqualTo(time.GetUtcNow().AddHours(-1));
        await Assert.That(leaf.Workspace).IsSameReferenceAs(workspace);
    }
}
