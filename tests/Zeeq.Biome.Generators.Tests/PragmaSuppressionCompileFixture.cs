using Zeeq.Biome;

namespace Zeeq.Biome.Generators.Tests.PragmaSuppressionCompileFixture;

internal sealed class Seed { }

internal sealed class News { }

[BiomeGraft]
internal static class Blueprints
{
#pragma warning disable BIOME002
    [Graft(Plural = "News")]
    internal static readonly Blueprint<Seed, News> News = null!;
#pragma warning restore BIOME002
}

internal static class Usage
{
    internal static void Build(BiomeBuilder<Seed, Seed> builder) => builder.AddNews(out _);
}
