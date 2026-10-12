using Zeeq.Biome;

namespace Zeeq.Biome.Tests.ProjectManagement;

internal sealed record ProjectSeed(string Owner);

internal sealed class Workspace
{
    public string Owner { get; init; } = "";
}

internal sealed class Project
{
    public Workspace Workspace { get; init; } = null!;
}

internal sealed class Board
{
    public Project Project { get; init; } = null!;
}

internal sealed class Column
{
    public Board Board { get; init; } = null!;
    public string Rank { get; set; } = null!;
    public string? Name { get; set; }
}

internal sealed class Card
{
    public Column Column { get; init; } = null!;
    public Workspace Workspace { get; init; } = null!;
    public string Rank { get; set; } = null!;
    public int Ticket { get; set; }
    public string? Title { get; set; }
    public bool Blocked { get; set; }
    public bool Urgent { get; set; }
    public DateTimeOffset DueAt { get; set; }
}

internal sealed class ChecklistItem
{
    public Card Card { get; init; } = null!;
    public Workspace Workspace { get; init; } = null!;
    public string Rank { get; set; } = null!;
    public string? Text { get; set; }
}

internal sealed class Comment
{
    public Card Card { get; init; } = null!;
    public Workspace Workspace { get; init; } = null!;
    public int Number { get; set; }
    public string? Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class Cards
{
    // These plain delegates compose into one customized card with +.
    internal static readonly Action<Card> Blocked = c => c.Blocked = true;
    internal static readonly Action<Card> Urgent = c => c.Urgent = true;
}

internal static class Blueprints
{
    // Each level owns a distinct definition; scopes are immediate parent identity, not array index.
    internal static readonly Sequence<string> ColumnRanks = Sequence
        .Of("a", r => r + "a")
        .PerParent();

    // A new column restarts card ranks while Under(column) resumes its progression.
    internal static readonly Sequence<string> CardRanks = Sequence
        .Of("a", r => r + "a")
        .PerParent();

    // Checklist ordering is independent for each card.
    internal static readonly Sequence<string> ChecklistRanks = Sequence
        .Of("a", r => r + "a")
        .PerParent();

    // Ticket numbers span columns/boards/projects, reaching the nearest workspace ancestor.
    internal static readonly Sequence<int> Tickets = Sequence.Of(1, n => n + 1).Per<Workspace>();

    // Comment numbering is another independent sequence under the card anchor.
    internal static readonly Sequence<int> CommentNumbers = Sequence.Of(1, n => n + 1).PerParent();

    // Seed values enter the graph at its root.
    internal static readonly Blueprint<ProjectSeed, Workspace> Workspace = Blueprint.For<
        ProjectSeed,
        Workspace
    >(ctx => new() { Owner = ctx.Seed.Owner });

    // Child factories wire the typed parent directly.
    internal static readonly ChildBlueprint<ProjectSeed, Workspace, Project> Project =
        Blueprint.ChildOf<ProjectSeed, Workspace, Project>(
            (workspace, _) => new() { Workspace = workspace }
        );

    // Every Add changes the anchor, so the next factory receives a project, not a workspace.
    internal static readonly ChildBlueprint<ProjectSeed, Project, Board> Board = Blueprint.ChildOf<
        ProjectSeed,
        Project,
        Board
    >((project, _) => new() { Project = project });

    // Rank is deliberately null (not empty): null is unset for a string Derive.
    internal static readonly ChildBlueprint<ProjectSeed, Board, Column> Column = Blueprint
        .ChildOf<ProjectSeed, Board, Column>((board, _) => new() { Board = board })
        .Derive(c => c.Rank, ColumnRanks)
        .Derive(c => c.Name, (c, _) => $"Column {c.Rank}");

    // A deep ancestor supplies tenancy; a Derive chain supplies a title from the ticket.
    internal static readonly ChildBlueprint<ProjectSeed, Column, Card> Card = Blueprint
        .ChildOf<ProjectSeed, Column, Card>(
            (column, ctx) =>
                new()
                {
                    Column = column,
                    Workspace = ctx.Ancestor<Workspace>(),
                    DueAt = ctx.FromNow(TimeSpan.FromDays(1)),
                }
        )
        .Derive(c => c.Rank, CardRanks)
        .Derive(c => c.Ticket, Tickets)
        .Derive(c => c.Title, (c, _) => $"Ticket {c.Ticket}");

    // Reach five edges up to Workspace without any runtime ancestor auto-creation.
    internal static readonly ChildBlueprint<ProjectSeed, Card, ChecklistItem> Checklist = Blueprint
        .ChildOf<ProjectSeed, Card, ChecklistItem>(
            (card, ctx) => new() { Card = card, Workspace = ctx.Ancestor<Workspace>() }
        )
        .Derive(i => i.Rank, ChecklistRanks)
        .Derive(i => i.Text, (i, _) => $"Task {i.Rank}");

    // Comments and checklist items are sibling branches; callers must Under(cards) to switch.
    internal static readonly ChildBlueprint<ProjectSeed, Card, Comment> Comment = Blueprint
        .ChildOf<ProjectSeed, Card, Comment>(
            (card, ctx) =>
                new()
                {
                    Card = card,
                    Workspace = ctx.Ancestor<Workspace>(),
                    CreatedAt = ctx.Ago(TimeSpan.FromHours(1)),
                }
        )
        .Derive(c => c.Number, CommentNumbers)
        .Derive(c => c.Text, (c, _) => $"Comment {c.Number}");
}

internal static class TestBiome
{
    private static readonly BiomeOptions<ProjectSeed> Options = new();

    // Passing a clock keeps setup and a caller's SUT on the same timeline.
    internal static BiomeBuilder<ProjectSeed, ProjectSeed> Start(
        out ProjectSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null
    )
    {
        seed = new("owner");
        return Biome.Start(seed, Options, store, time);
    }
}
