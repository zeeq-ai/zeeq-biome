using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Board;

internal static class Sugar
{
    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<BoardSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<BoardSeed, Board[]> AddBoard(out Board entity) =>
            b.AddOne(Blueprints.Board, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<BoardSeed, Board[]> AddBoard(
            out Board entity,
            Action<Board> customize
        ) => b.AddOne(Blueprints.Board, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<BoardSeed, Board[]> AddBoards(out Board[] entities, int count) =>
            b.Add(Blueprints.Board, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<BoardSeed, Board[]> AddBoards(
            out Board[] entities,
            params Action<Board>[] customize
        ) => b.Add(Blueprints.Board, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<BoardSeed, Board[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<BoardSeed, Column[]> AddColumn(out Column entity) =>
            b.AddOne(Blueprints.Column, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<BoardSeed, Column[]> AddColumn(
            out Column entity,
            Action<Column> customize
        ) => b.AddOne(Blueprints.Column, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<BoardSeed, Column[]> AddColumns(out Column[] entities, int count) =>
            b.Add(Blueprints.Column, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<BoardSeed, Column[]> AddColumns(
            out Column[] entities,
            params Action<Column>[] customize
        ) => b.Add(Blueprints.Column, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<BoardSeed, Column[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<BoardSeed, Card[]> AddCard(out Card entity) =>
            b.AddOne(Blueprints.Card, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<BoardSeed, Card[]> AddCard(out Card entity, Action<Card> customize) =>
            b.AddOne(Blueprints.Card, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<BoardSeed, Card[]> AddCards(out Card[] entities, int count) =>
            b.Add(Blueprints.Card, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<BoardSeed, Card[]> AddCards(
            out Card[] entities,
            params Action<Card>[] customize
        ) => b.Add(Blueprints.Card, out entities, customize);
    }
}
