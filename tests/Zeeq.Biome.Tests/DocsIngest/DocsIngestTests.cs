using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.DocsIngest;

public sealed class DocsIngestTests
{
    [Test]
    public async Task Search_corpus_fans_out_and_ingest_aggregate_expands_after_document_rows()
    {
        // A searchable corpus: four levels plus an aggregate run with one status per document.
        // Arrange: keep the store to observe expansion order separately from wrapper inspection.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store);

        // Act: the long chain creates the corpus; the root input blueprint can run from any anchor.
        await biome
            .AddLibraries(out var libraries, 2)
            // 2 libraries × 3 documents = 6 documents.
            .AddDocuments(out var documents, 3)
            // 6 documents × 4 sections = 24 sections.
            .AddSections(out var sections, 4)
            // 24 sections × 3 chunks = 72 deepest leaves.
            .AddChunks(out var chunks, 3)
            // Under documents marks the conceptual sibling branch; input customizers select its rows.
            .Under(documents)
            .AddIngestRun(out var graph, i => i.Documents = documents)
            .BuildAsync();

        // Assert: PerParent ordinals restart, Per<Library> ordinals cross document/section boundaries.
        await Assert.That(biome.All<Library>().Count).IsEqualTo(2);
        await Assert.That(biome.All<Document>().Count).IsEqualTo(2 * 3);
        await Assert.That(biome.All<Section>().Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(biome.All<Chunk>().Count).IsEqualTo(2 * 3 * 4 * 3);
        await Assert.That(biome.All<IngestGraph>().Count).IsEqualTo(1);
        await Assert.That(chunks[3].Ordinal).IsEqualTo(1);
        await Assert.That(chunks[3].LibraryOrdinal).IsEqualTo(4);
        await Assert.That(chunks[36].LibraryOrdinal).IsEqualTo(1);
        await Assert.That(chunks[71].Library).IsSameReferenceAs(libraries[1]);
        await Assert.That(sections[4].Number).IsEqualTo(1);
        await Assert.That(documents[3].Version).IsEqualTo(1);
        await Assert.That(store.Entities.Count).IsEqualTo(2 + 6 + 24 + 72 + 1 + 6);
        await Assert.That(store.Entities[104]).IsSameReferenceAs(graph.Run);
        await Assert.That(store.Entities[105]).IsSameReferenceAs(graph.Statuses[0]);
        await Assert.That(graph.Statuses[5].Document).IsSameReferenceAs(documents[5]);
        await Assert.That(store.Entities.OfType<IngestGraph>().Any()).IsFalse();
        await Assert
            .That(graph.Run.StartedAt - documents[0].UpdatedAt)
            .IsEqualTo(TimeSpan.FromHours(1));
    }

    [Test]
    public async Task Archived_sensitive_documents_skip_explicit_versions_and_resume_sibling_branches()
    {
        // An archived private corpus: stacked traits, an explicit version, and a later sibling ingest.
        // Arrange: versions are scoped on the definition, so blueprint variants share progression.
        var biome = TestBiome.Start(out _);

        // Act: every customizer is applied PER library; + describes one document with both traits.
        await biome
            .AddLibraries(out var libraries, 2)
            .AddDocuments(
                out var documents,
                Documents.Archived + Documents.Sensitive,
                // Explicit 1 is reserved first; the other document gets 2 and derives guide-v2.md.
                d =>
                {
                    d.Version = 1;
                    d.Path = "contract.md";
                }
            )
            .AddSections(out var sections, 2)
            .AddChunks(out _, 2)
            // Resume both libraries after their deep descendant branches.
            .Under(libraries)
            .AddDocuments(out var later, 1)
            // Revisit one section: local ordinals continue, rather than resetting on this Add call.
            .Under(sections[0])
            .AddChunk(out var extra)
            .BuildAsync();

        // Assert: call site wins, nothing overwrites the explicit path.
        await Assert.That(documents[0].Archived && documents[0].Sensitive).IsTrue();
        await Assert.That(documents[0].Version).IsEqualTo(2);
        await Assert.That(documents[0].Path).IsEqualTo("guide-v2.md");
        await Assert.That(documents[1].Path).IsEqualTo("contract.md");
        await Assert.That(later.All(d => d.Version == 3)).IsTrue();
        await Assert.That(extra.Ordinal).IsEqualTo(3);
        await Assert.That(extra.LibraryOrdinal).IsEqualTo(9);
        await Assert.That(extra.Text).IsEqualTo("Section 1: chunk 3");
        await Assert.That(biome.All<Chunk>().Count).IsEqualTo(2 * 2 * 2 * 2 + 1);
    }

    [Test]
    public async Task Searchable_chunk_helper_and_transient_ingest_preview_use_live_time()
    {
        // A default searchable leaf plus a preview aggregate: live time and recursive transient exclusion.
        // Arrange: a helper gives the leaf without losing inspectable ancestors.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(instant);
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store, time);
        biome.AddSearchableChunk(out var leaf);

        // Act: advance the shared provider before building the preview run.
        time.Advance(TimeSpan.FromMinutes(10));
        await biome
            .Under(leaf.Section.Document)
            .AddIngestRun(out var preview, i => i.Documents = [leaf.Section.Document])
            // Built and returned, but NOT saved; aggregate parts inherit the excluded branch.
            .Transient()
            .BuildAsync();

        // Assert: All observes the wrapper; the store receives neither its run nor its status rows.
        await Assert.That(biome.All<IngestGraph>().Count).IsEqualTo(1);
        await Assert.That(biome.All<Chunk>().Count).IsEqualTo(1);
        await Assert.That(store.Entities.Count).IsEqualTo(4);
        await Assert.That(store.Entities.OfType<IngestRun>().Any()).IsFalse();
        await Assert.That(store.Entities.OfType<DocumentStatus>().Any()).IsFalse();
        await Assert.That(preview.Run.StartedAt).IsEqualTo(instant.AddMinutes(10));
        await Assert.That(preview.Run.RetryAt).IsEqualTo(instant.AddMinutes(15));
        await Assert.That(leaf.Section.Document.UpdatedAt).IsEqualTo(instant.AddHours(-1));
    }
}
