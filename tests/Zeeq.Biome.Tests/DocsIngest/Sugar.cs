using Zeeq.Biome;

namespace Zeeq.Biome.Tests.DocsIngest;

internal static class Sugar
{
    // Root/input sugar works from any anchor; no implicit ancestor construction is needed.
    extension<TAnchor>(BiomeBuilder<IngestSeed, TAnchor> b)
    {
        /// <summary>Builds one default Library.</summary>
        public BiomeBuilder<IngestSeed, Library[]> AddLibrary(out Library entity) =>
            b.AddOne(Blueprints.Library, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<IngestSeed, Library[]> AddLibrary(
            out Library entity,
            Action<Library> customize
        ) => b.AddOne(Blueprints.Library, out entity, customize);

        /// <summary>Builds count outputs.</summary>
        public BiomeBuilder<IngestSeed, Library[]> AddLibraries(
            out Library[] entities,
            int count
        ) => b.Add(Blueprints.Library, out entities, count);

        /// <summary>Builds one output per delegate; use + to stack traits.</summary>
        public BiomeBuilder<IngestSeed, Library[]> AddLibraries(
            out Library[] entities,
            params Action<Library>[] customize
        ) => b.Add(Blueprints.Library, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<IngestSeed, Library[]> b)
    {
        /// <summary>Builds one default Document under exactly one Library.</summary>
        public BiomeBuilder<IngestSeed, Document[]> AddDocument(out Document entity) =>
            b.AddOne(Blueprints.Document, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<IngestSeed, Document[]> AddDocument(
            out Document entity,
            Action<Document> customize
        ) => b.AddOne(Blueprints.Document, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<IngestSeed, Document[]> AddDocuments(
            out Document[] entities,
            int count
        ) => b.Add(Blueprints.Document, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<IngestSeed, Document[]> AddDocuments(
            out Document[] entities,
            params Action<Document>[] customize
        ) => b.Add(Blueprints.Document, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<IngestSeed, Document[]> b)
    {
        /// <summary>Builds one default Section under exactly one Document.</summary>
        public BiomeBuilder<IngestSeed, Section[]> AddSection(out Section entity) =>
            b.AddOne(Blueprints.Section, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<IngestSeed, Section[]> AddSection(
            out Section entity,
            Action<Section> customize
        ) => b.AddOne(Blueprints.Section, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<IngestSeed, Section[]> AddSections(out Section[] entities, int count) =>
            b.Add(Blueprints.Section, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<IngestSeed, Section[]> AddSections(
            out Section[] entities,
            params Action<Section>[] customize
        ) => b.Add(Blueprints.Section, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<IngestSeed, Section[]> b)
    {
        /// <summary>Builds one default Chunk under exactly one Section.</summary>
        public BiomeBuilder<IngestSeed, Chunk[]> AddChunk(out Chunk entity) =>
            b.AddOne(Blueprints.Chunk, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<IngestSeed, Chunk[]> AddChunk(
            out Chunk entity,
            Action<Chunk> customize
        ) => b.AddOne(Blueprints.Chunk, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<IngestSeed, Chunk[]> AddChunks(out Chunk[] entities, int count) =>
            b.Add(Blueprints.Chunk, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<IngestSeed, Chunk[]> AddChunks(
            out Chunk[] entities,
            params Action<Chunk>[] customize
        ) => b.Add(Blueprints.Chunk, out entities, customize);
    }

    // Root/input sugar works from any anchor; no implicit ancestor construction is needed.
    extension<TAnchor>(BiomeBuilder<IngestSeed, TAnchor> b)
    {
        /// <summary>Builds one default IngestGraph.</summary>
        public BiomeBuilder<IngestSeed, IngestGraph[]> AddIngestRun(out IngestGraph entity) =>
            b.AddOne(Blueprints.Ingest, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<IngestSeed, IngestGraph[]> AddIngestRun(
            out IngestGraph entity,
            Action<IngestInput> customize
        ) => b.AddOne(Blueprints.Ingest, out entity, customize);

        /// <summary>Builds count outputs.</summary>
        public BiomeBuilder<IngestSeed, IngestGraph[]> AddIngestRuns(
            out IngestGraph[] entities,
            int count
        ) => b.Add(Blueprints.Ingest, out entities, count);

        /// <summary>Builds one output per delegate; use + to stack traits.</summary>
        public BiomeBuilder<IngestSeed, IngestGraph[]> AddIngestRuns(
            out IngestGraph[] entities,
            params Action<IngestInput>[] customize
        ) => b.Add(Blueprints.Ingest, out entities, customize);
    }

    // Scenario helpers are explicit app recipes, available only from the seed anchor.
    extension(BiomeBuilder<IngestSeed, IngestSeed> b)
    {
        /// <summary>Builds a complete default 4-level branch and returns its leaf.</summary>
        public BiomeBuilder<IngestSeed, Chunk[]> AddSearchableChunk(out Chunk leaf) =>
            b.AddLibrary(out _).AddDocument(out _).AddSection(out _).AddChunk(out leaf);
    }
}
