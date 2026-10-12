using Zeeq.Biome;

namespace Zeeq.Biome.Tests.DocsIngest;

internal sealed record IngestSeed(string Owner);

internal sealed class Library
{
    public string Owner { get; init; } = "";
}

internal sealed class Document
{
    public Library Library { get; init; } = null!;
    public int Version { get; set; }
    public string? Path { get; set; }
    public bool Archived { get; set; }
    public bool Sensitive { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class Section
{
    public Document Document { get; init; } = null!;
    public int Number { get; set; }
    public string? Heading { get; set; }
}

internal sealed class Chunk
{
    public Section Section { get; init; } = null!;
    public Library Library { get; init; } = null!;
    public int Ordinal { get; set; }
    public int LibraryOrdinal { get; set; }
    public string? Text { get; set; }
}

internal sealed class IngestInput
{
    public Document[] Documents { get; set; } = [];
    public string? Label { get; set; }
}

internal sealed record IngestRun(string Label, DateTimeOffset StartedAt, DateTimeOffset RetryAt);

internal sealed record DocumentStatus(IngestRun Run, Document Document);

internal sealed record IngestGraph(IngestRun Run, DocumentStatus[] Statuses) : IBiomeAggregate
{
    // Expansion preserves run-before-status order; the wrapper itself is never a row.
    public IEnumerable<object> GetEntities() => [Run, .. Statuses];
}

internal static class Documents
{
    // Combine policy traits on one document; customizers can still override them.
    internal static readonly Action<Document> Archived = d => d.Archived = true;
    internal static readonly Action<Document> Sensitive = d => d.Sensitive = true;
}

internal static class Blueprints
{
    // Versions belong to a library, not a blueprint variant or Add call.
    internal static readonly Sequence<int> Versions = Sequence.Of(1, n => n + 1).Per<Library>();

    // Section numbering restarts for each document.
    internal static readonly Sequence<int> SectionNumbers = Sequence.Of(1, n => n + 1).PerParent();

    // Chunk ordinals restart for each section.
    internal static readonly Sequence<int> ChunkOrdinals = Sequence.Of(1, n => n + 1).PerParent();

    // This contrasting scope numbers chunks across every document/section in one library.
    internal static readonly Sequence<int> LibraryOrdinals = Sequence
        .Of(1, n => n + 1)
        .Per<Library>();

    // Root factories introduce ownership from the seed.
    internal static readonly Blueprint<IngestSeed, Library> Library = Blueprint.For<
        IngestSeed,
        Library
    >(ctx => new() { Owner = ctx.Seed.Owner });

    // Leave version/path unset; derive the path from the final version after overrides.
    internal static readonly ChildBlueprint<IngestSeed, Library, Document> Document = Blueprint
        .ChildOf<IngestSeed, Library, Document>(
            (library, ctx) =>
                new() { Library = library, UpdatedAt = ctx.Ago(TimeSpan.FromHours(1)) }
        )
        .Derive(d => d.Version, Versions)
        .Derive(d => d.Path, (d, _) => $"guide-v{d.Version}.md");

    // A section heading depends on the sequence-assigned section number.
    internal static readonly ChildBlueprint<IngestSeed, Document, Section> Section = Blueprint
        .ChildOf<IngestSeed, Document, Section>((document, _) => new() { Document = document })
        .Derive(s => s.Number, SectionNumbers)
        .Derive(s => s.Heading, (s, _) => $"Section {s.Number}");

    // Reach Library through Section -> Document; keep local and library-wide ordinals distinct.
    internal static readonly ChildBlueprint<IngestSeed, Section, Chunk> Chunk = Blueprint
        .ChildOf<IngestSeed, Section, Chunk>(
            (section, ctx) => new() { Section = section, Library = ctx.Ancestor<Library>() }
        )
        .Derive(c => c.Ordinal, ChunkOrdinals)
        .Derive(c => c.LibraryOrdinal, LibraryOrdinals)
        .Derive(c => c.Text, (c, _) => $"{c.Section.Heading}: chunk {c.Ordinal}");

    // Input customizers choose documents; expansion yields one run and one status per document.
    internal static readonly InputBlueprint<IngestSeed, IngestInput, IngestGraph> Ingest = Blueprint
        .FromInput<IngestSeed, IngestInput, IngestGraph>(
            () => new(),
            (input, ctx) =>
            {
                var run = new IngestRun(
                    input.Label!,
                    ctx.Now,
                    ctx.FromNow(TimeSpan.FromMinutes(5))
                );
                return new(run, input.Documents.Select(d => new DocumentStatus(run, d)).ToArray());
            }
        )
        .Derive(i => i.Label, (_, _) => "ingest");
}

internal static class TestBiome
{
    private static readonly BiomeOptions<IngestSeed> Options = new();

    // A recording store makes graph construction and aggregate persistence separately observable.
    internal static BiomeBuilder<IngestSeed, IngestSeed> Start(
        out IngestSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null
    )
    {
        seed = new("owner");
        return Biome.Start(seed, Options, store, time);
    }
}
