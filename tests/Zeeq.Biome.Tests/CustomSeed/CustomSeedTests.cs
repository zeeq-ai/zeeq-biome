using System.Collections.Concurrent;
using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CustomSeed;

public sealed class CustomSeedTests
{
    [Test]
    public async Task Plain_record_seed_interface_defaults_and_save_hooks_work_together()
    {
        // Custom seed shape: interface defaults supply ownership and hooks add an audit row around persistence.
        // Arrange: Configure interface ownership plus hooks that log order and add an audit row.
        var events = new List<string>();
        var store = new RecordingStore { OnSave = () => events.Add("save") };
        var options = TestBiome
            .NewOptions()
            .BeforeSave(
                (ctx, _) =>
                {
                    events.Add("before");
                    ctx.Entities.Add(new Audit(ctx.Seed.Actor));
                    return ValueTask.CompletedTask;
                }
            )
            .AfterSave(
                (ctx, _) =>
                {
                    events.Add("after");
                    events.Add(ctx.Session.Last<Note>().Text);
                    return ValueTask.CompletedTask;
                }
            );
        var biome = TestBiome.Start(out var seed, store, options);

        // Act: Create a default-owned note and an explicitly owned note, then save.
        await biome.AddNotes(out var notes, _ => { }, n => n.CreatedBy = "custom").BuildAsync();

        // Assert: Explicit ownership survives; before/save/after run in order and the audit row reaches the store.
        await Assert.That(notes[0].CreatedBy).IsEqualTo(seed.Actor);
        await Assert.That(notes[1].CreatedBy).IsEqualTo("custom");
        await Assert.That(store.Entities.OfType<Audit>().Single().Actor).IsEqualTo(seed.Actor);
        await Assert.That(string.Join(",", events)).IsEqualTo("before,save,after,note");
    }

    [Test]
    public void Frozen_options_throw_on_every_configuration_mutator()
    {
        // Shared configuration: first use freezes options, preventing later mutation from leaking between tests.
        // Arrange: Use shared options once through the domain wrapper to freeze them.
        var options = TestBiome.NewOptions();

        // Act: Attempt every supported configuration mutation after first use.
        TestBiome.Start(out _, options: options);

        // Assert: Every mutation throws; a running scenario cannot change shared configuration.
        Assert.Throws<InvalidOperationException>(() => options.Ids(p => p));
        Assert.Throws<InvalidOperationException>(() => options.TimePrecision(TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => options.SeedEntities(_ => []));
        Assert.Throws<InvalidOperationException>(() =>
            options.Default<IHasCreatedBy, string?>(n => n.CreatedBy, (_, _) => "x")
        );
        Assert.Throws<InvalidOperationException>(() =>
            options.BeforeSave((_, _) => ValueTask.CompletedTask)
        );
        Assert.Throws<InvalidOperationException>(() =>
            options.AfterSave((_, _) => ValueTask.CompletedTask)
        );
    }

    [Test]
    public async Task Shared_options_blueprints_and_sequences_have_no_cross_session_state()
    {
        // Parallel scenarios: immutable definitions are shared, but ownership and sequence state belong to each session.
        // Arrange: Share definitions, but collect outputs from 32 independent sessions.
        var options = TestBiome.NewOptions();
        var scenarios = new ConcurrentBag<(CustomSeed Seed, Note[] Notes)>();

        // Act: Run up to eight sessions concurrently with three notes each.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 32),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (tenant, ct) =>
            {
                await Task.Yield();
                var seed = new CustomSeed($"actor-{tenant}", tenant);
                var biome = Biome.Start(seed, options);
                await biome.AddNotes(out var notes, 3).BuildAsync(ct);
                scenarios.Add((seed, notes));
            }
        );

        foreach (var (seed, notes) in scenarios)
        {
            await Assert.That(notes.Select(n => n.Number).ToArray()).IsEquivalentTo([1, 2, 3]);
            await Assert.That(notes.All(n => n.CreatedBy == seed.Actor)).IsTrue();
        }

        // Assert: Every session starts at 1, retains its actor, and all 96 IDs are distinct.
        await Assert
            .That(scenarios.SelectMany(s => s.Notes).Select(n => n.Id).Distinct().Count())
            .IsEqualTo(96);
    }

    [Test]
    public async Task Session_sequences_continue_across_root_adds_and_custom_ids_are_used()
    {
        // Independent root adds: session-scoped numbering continues and factories use the configured ID generator.
        // Arrange: Override the ID function while retaining the shared session-scoped sequence.
        var options = TestBiome.NewOptions().Ids(prefix => $"{prefix}_known");

        // Act: Perform a singular root add followed by a plural root add.
        await TestBiome
            .Start(out _, options: options)
            // Factories use configured IDs and leave CreatedBy for the interface default.
            .AddNote(out var first)
            // Session-scoped numbering continues; an explicit CreatedBy survives interface defaults.
            .AddNotes(out var rest, 2)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Numbers continue across adds and factories honor the ID override.
        await Assert.That(first.Number).IsEqualTo(1);
        await Assert.That(rest[0].Number).IsEqualTo(2);
        await Assert.That(first.Id).IsEqualTo("note_known");
    }
}
