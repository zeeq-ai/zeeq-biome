using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.DocsIngest;

// The same scenarios as DocsIngestTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class DocsIngestTests
{
    [Test]
    public async Task Search_corpus_fans_out_and_ingest_aggregate_expands_after_document_rows_typical()
    {
        // Arrange: four nested loops with a per-library version and chunk counter.
        var now = DateTimeOffset.UtcNow;
        var libraries = new List<Library>();
        var documents = new List<Document>();
        var sections = new List<Section>();
        var chunks = new List<Chunk>();

        for (var l = 0; l < 2; l++)
        {
            var library = new Library { Owner = "owner" };
            libraries.Add(library);
            var version = 1; // per library
            var libraryOrdinal = 1; // per library, across documents and sections
            for (var d = 0; d < 3; d++)
            {
                var document = new Document
                {
                    Library = library,
                    Version = version,
                    Path = $"guide-v{version++}.md",
                    UpdatedAt = now.AddHours(-1),
                };
                documents.Add(document);
                for (var s = 1; s <= 4; s++)
                {
                    var section = new Section
                    {
                        Document = document,
                        Number = s,
                        Heading = $"Section {s}",
                    };
                    sections.Add(section);
                    for (var c = 1; c <= 3; c++)
                    {
                        chunks.Add(
                            new Chunk
                            {
                                Section = section,
                                Library = library,
                                Ordinal = c,
                                LibraryOrdinal = libraryOrdinal++,
                                Text = $"{section.Heading}: chunk {c}",
                            }
                        );
                    }
                }
            }
        }

        // The ingest run needs one status per document; the wrapper itself is not a row.
        var run = new IngestRun("ingest", now, now.AddMinutes(5));
        var graph = new IngestGraph(
            run,
            documents.Select(d => new DocumentStatus(run, d)).ToArray()
        );
        var saved = new List<object>();
        saved.AddRange(libraries);
        saved.AddRange(documents);
        saved.AddRange(sections);
        saved.AddRange(chunks);
        saved.AddRange(graph.GetEntities());

        // Assert: identical to the Biome version.
        await Assert.That(libraries.Count).IsEqualTo(2);
        await Assert.That(documents.Count).IsEqualTo(2 * 3);
        await Assert.That(sections.Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(chunks.Count).IsEqualTo(2 * 3 * 4 * 3);
        await Assert.That(chunks[3].Ordinal).IsEqualTo(1);
        await Assert.That(chunks[3].LibraryOrdinal).IsEqualTo(4);
        await Assert.That(chunks[36].LibraryOrdinal).IsEqualTo(1);
        await Assert.That(chunks[71].Library).IsSameReferenceAs(libraries[1]);
        await Assert.That(sections[4].Number).IsEqualTo(1);
        await Assert.That(documents[3].Version).IsEqualTo(1);
        await Assert.That(saved.Count).IsEqualTo(2 + 6 + 24 + 72 + 1 + 6);
        await Assert.That(saved[104]).IsSameReferenceAs(graph.Run);
        await Assert.That(saved[105]).IsSameReferenceAs(graph.Statuses[0]);
        await Assert.That(graph.Statuses[5].Document).IsSameReferenceAs(documents[5]);
        await Assert.That(saved.OfType<IngestGraph>().Any()).IsFalse();
        await Assert
            .That(graph.Run.StartedAt - documents[0].UpdatedAt)
            .IsEqualTo(TimeSpan.FromHours(1));
    }

    [Test]
    public async Task Archived_sensitive_documents_skip_explicit_versions_and_resume_sibling_branches_typical()
    {
        // Arrange: reserve version 1 by hand, so the other document in each library takes 2.
        var now = DateTimeOffset.UtcNow;
        var libraries = new[]
        {
            new Library { Owner = "owner" },
            new Library { Owner = "owner" },
        };
        var documents = libraries
            .SelectMany(l =>
                new[]
                {
                    new Document
                    {
                        Library = l,
                        Version = 2,
                        Path = "guide-v2.md",
                        Archived = true,
                        Sensitive = true,
                        UpdatedAt = now.AddHours(-1),
                    },
                    new Document
                    {
                        Library = l,
                        Version = 1,
                        Path = "contract.md",
                        UpdatedAt = now.AddHours(-1),
                    },
                }
            )
            .ToList();
        // Resume each library's version sequence after its two documents.
        var later = libraries
            .Select(l => new Document
            {
                Library = l,
                Version = 3,
                Path = "guide-v3.md",
                UpdatedAt = now.AddHours(-1),
            })
            .ToList();

        var sections = documents
            .SelectMany(d =>
                Enumerable
                    .Range(1, 2)
                    .Select(n => new Section
                    {
                        Document = d,
                        Number = n,
                        Heading = $"Section {n}",
                    })
            )
            .ToList();
        // The library ordinal runs across every document and section in the library.
        var libraryOrdinals = libraries.ToDictionary(l => l, _ => 1);
        var chunks = sections
            .SelectMany(s =>
                Enumerable
                    .Range(1, 2)
                    .Select(n => new Chunk
                    {
                        Section = s,
                        Library = s.Document.Library,
                        Ordinal = n,
                        LibraryOrdinal = libraryOrdinals[s.Document.Library]++,
                        Text = $"{s.Heading}: chunk {n}",
                    })
            )
            .ToList();
        // Revisit the first section: ordinal 3 locally, and 9 across library 0's eight chunks.
        var extra = new Chunk
        {
            Section = sections[0],
            Library = libraries[0],
            Ordinal = 3,
            LibraryOrdinal = 9,
            Text = "Section 1: chunk 3",
        };
        chunks.Add(extra);

        // Assert: identical to the Biome version.
        await Assert.That(documents[0].Archived && documents[0].Sensitive).IsTrue();
        await Assert.That(documents[0].Version).IsEqualTo(2);
        await Assert.That(documents[0].Path).IsEqualTo("guide-v2.md");
        await Assert.That(documents[1].Path).IsEqualTo("contract.md");
        await Assert.That(later.All(d => d.Version == 3)).IsTrue();
        await Assert.That(extra.Ordinal).IsEqualTo(3);
        await Assert.That(extra.LibraryOrdinal).IsEqualTo(9);
        await Assert.That(extra.Text).IsEqualTo("Section 1: chunk 3");
        await Assert.That(chunks.Count).IsEqualTo(2 * 2 * 2 * 2 + 1);
    }

    [Test]
    public async Task Searchable_chunk_helper_and_transient_ingest_preview_use_live_time_typical()
    {
        // Arrange: no helper, so the four-level branch is written out in the test.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(instant);
        var library = new Library { Owner = "owner" };
        var document = new Document
        {
            Library = library,
            Version = 1,
            Path = "guide-v1.md",
            UpdatedAt = time.GetUtcNow().AddHours(-1),
        };
        var section = new Section
        {
            Document = document,
            Number = 1,
            Heading = "Section 1",
        };
        var leaf = new Chunk
        {
            Section = section,
            Library = library,
            Ordinal = 1,
            LibraryOrdinal = 1,
            Text = "Section 1: chunk 1",
        };
        var saved = new List<object> { library, document, section, leaf };

        // Act: advance the clock, then build the preview run. It is never added to saved.
        time.Advance(TimeSpan.FromMinutes(10));
        var run = new IngestRun("ingest", time.GetUtcNow(), time.GetUtcNow().AddMinutes(5));
        var preview = new IngestGraph(run, [new DocumentStatus(run, document)]);

        // Assert: identical to the Biome version.
        await Assert.That(preview.Statuses.Length).IsEqualTo(1);
        await Assert.That(saved.Count).IsEqualTo(4);
        await Assert.That(saved.OfType<IngestRun>().Any()).IsFalse();
        await Assert.That(saved.OfType<DocumentStatus>().Any()).IsFalse();
        await Assert.That(preview.Run.StartedAt).IsEqualTo(instant.AddMinutes(10));
        await Assert.That(preview.Run.RetryAt).IsEqualTo(instant.AddMinutes(15));
        await Assert.That(document.UpdatedAt).IsEqualTo(instant.AddHours(-1));
    }
}
