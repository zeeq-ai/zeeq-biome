using System.Globalization;

namespace Zeeq.Biome.Tests.Regression;

public sealed class IdTests
{
    [Test]
    public async Task Default_ids_keep_prefix_uuid_version_variant_and_timestamp_on_every_target()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var blueprint = Blueprint.For<string, Row>(ctx => new() { Id = ctx.NewId("row") });
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        biome.Add(blueprint, out var rows, 128);
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var row in rows)
        {
            await Assert.That(row.Id.StartsWith("row_", StringComparison.Ordinal)).IsTrue();
            var text = Guid.ParseExact(row.Id[4..], "N").ToString("N");
            await Assert.That(text[12]).IsEqualTo('7');
            await Assert.That("89ab".Contains(text[16], StringComparison.Ordinal)).IsTrue();
            var milliseconds = long.Parse(
                text[..12],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture
            );
            await Assert.That(milliseconds >= before && milliseconds <= after).IsTrue();
        }
        await Assert.That(rows.Select(row => row.Id).Distinct().Count()).IsEqualTo(rows.Length);
    }

    private sealed class Row
    {
        public required string Id { get; init; }
    }
}
