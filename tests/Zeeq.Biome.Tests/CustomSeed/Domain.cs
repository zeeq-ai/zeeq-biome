using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CustomSeed;

internal sealed record CustomSeed(string Actor, int Tenant);

internal interface IHasCreatedBy
{
    string? CreatedBy { get; set; }
}

internal sealed class Note : IHasCreatedBy
{
    public string? CreatedBy { get; set; }
    public string? Id { get; set; }
    public int Number { get; set; }
    public string Text { get; set; } = "note";
}

internal sealed record Audit(string Actor);

internal static class Blueprints
{
    // PerSession spans every root Add; each test session has independent progression.
    internal static readonly Sequence<int> Numbers = Sequence.Of(1, n => n + 1).PerSession();

    // Set the ID in the factory, leave ownership to the interface default, and derive numbering.
    internal static readonly Blueprint<CustomSeed, Note> Note = Blueprint
        .For<CustomSeed, Note>(ctx => new() { Id = ctx.NewId("note") })
        .Derive(n => n.Number, Numbers);
}

internal static class TestBiome
{
    // Match an interface: ownership defaults apply across entity implementations and remain fill-only.
    internal static BiomeOptions<CustomSeed> NewOptions() =>
        new BiomeOptions<CustomSeed>().Default<IHasCreatedBy, string?>(
            n => n.CreatedBy,
            (_, ctx) => ctx.Seed.Actor
        );

    private static readonly BiomeOptions<CustomSeed> Options = NewOptions();

    // Domain wrapper supplies a fresh seed/session while sharing frozen configuration.
    internal static BiomeBuilder<CustomSeed, CustomSeed> Start(
        out CustomSeed seed,
        IBiomeStore? store = null,
        BiomeOptions<CustomSeed>? options = null
    )
    {
        seed = new("actor", 42);
        return Biome.Start(seed, options ?? Options, store);
    }
}
