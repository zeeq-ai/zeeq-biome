using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CustomSeed;

internal static class Sugar
{
    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<CustomSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<CustomSeed, Note[]> AddNote(out Note entity) =>
            b.AddOne(Blueprints.Note, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CustomSeed, Note[]> AddNote(out Note entity, Action<Note> customize) =>
            b.AddOne(Blueprints.Note, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<CustomSeed, Note[]> AddNotes(out Note[] entities, int count) =>
            b.Add(Blueprints.Note, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<CustomSeed, Note[]> AddNotes(
            out Note[] entities,
            params Action<Note>[] customize
        ) => b.Add(Blueprints.Note, out entities, customize);
    }
}
