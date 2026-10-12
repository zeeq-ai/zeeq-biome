using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Board;

internal sealed record BoardSeed(string Owner);

internal sealed class Board
{
    public string Name { get; set; } = "board";
}

internal sealed class Column
{
    public Board Board { get; init; } = null!;
    public string Name { get; set; } = "column";
}

internal sealed class Card
{
    public Column Column { get; init; } = null!;
    public string Rank { get; set; } = null!;
    public string Title { get; set; } = "card";
}

internal static class Blueprints
{
    // PerParent resumes for the same column reference; it restarts for a different column.
    internal static readonly Sequence<string> Ranks = Sequence
        .Of("a", rank => rank + "a")
        .PerParent();

    // The root factory introduces a board without implicit ancestor creation.
    internal static readonly Blueprint<BoardSeed, Board> Board = Blueprint.For<BoardSeed, Board>(
        _ => new()
    );

    // Child factory wires the board supplied by the exact anchor.
    internal static readonly ChildBlueprint<BoardSeed, Board, Column> Column = Blueprint.ChildOf<
        BoardSeed,
        Board,
        Column
    >((board, _) => new() { Board = board });

    // Rank stays null until Derive; empty string would count as explicitly set.
    internal static readonly ChildBlueprint<BoardSeed, Column, Card> Card = Blueprint
        .ChildOf<BoardSeed, Column, Card>((column, _) => new() { Column = column })
        .Derive(c => c.Rank, Ranks);
}

internal static class TestBiome
{
    private static readonly BiomeOptions<BoardSeed> Options = new();

    // Domain wrapper supplies a fresh seed/session while sharing frozen configuration.
    internal static BiomeBuilder<BoardSeed, BoardSeed> Start(
        out BoardSeed seed,
        IBiomeStore? store = null
    )
    {
        seed = new("owner");
        return Biome.Start(seed, Options, store);
    }
}
