using Microsoft.Extensions.Time.Testing;
using Zeeq.Biome;
using Zeeq.Biome.Tests.CodeReviews;

namespace Zeeq.Biome.Tests;

public sealed class PipelineTests
{
    private sealed class Row
    {
        public string? Value { get; set; }
        public int Number { get; set; }
        public int OtherNumber { get; set; }
        public bool Enabled { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    [Test]
    public async Task Factory_traits_customizer_derivations_defaults_and_recording_follow_contract()
    {
        // Pipeline precedence: factory -> ordered traits -> customizer -> fill-only derives -> defaults -> recording.
        // Arrange: Log factory, traits, derive and global-default callbacks on the same property.
        var events = new List<string>();
        var options = new BiomeOptions<string>().Default<Row, string?>(
            r => r.Value,
            (_, _) =>
            {
                events.Add("default");
                return "default";
            }
        );
        var blueprint = Blueprint
            .For<string, Row>(ctx =>
            {
                events.Add($"factory:{ctx.All<Row>().Count}");
                return new() { Value = "factory" };
            })
            .With(r =>
            {
                events.Add("trait1");
                r.Value = "trait1";
            })
            .With(
                (r, _) =>
                {
                    events.Add("trait2");
                    r.Value = "trait2";
                }
            )
            .Derive(
                r => r.Number,
                (_, _) =>
                {
                    events.Add("derive");
                    return 1;
                }
            )
            .Derive(
                r => r.Value,
                (_, _) =>
                {
                    events.Add("overwriting-derive");
                    return "derive";
                }
            );
        var biome = Biome.Start("seed", options);

        // Act: Set an explicit value in the call-site customizer before fill-only stages run.
        await biome
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(
                blueprint,
                out var row,
                r =>
                {
                    events.Add("customizer");
                    r.Value = "custom";
                }
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Ordered traits run first; call site wins, nothing overwrites, and the row is recorded last.
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo("factory:0,trait1,trait2,customizer,derive");
        await Assert.That(row.Value).IsEqualTo("custom");
        await Assert.That(biome.Last<Row>()).IsSameReferenceAs(row);
    }

    [Test]
    public async Task Derivation_is_more_specific_than_global_default_and_all_default_sentinels_fill()
    {
        // Fill-only precedence: blueprint derives beat global defaults; null/zero/false/default time mean unset.
        // Arrange: Configure defaults for reference, numeric, boolean and timestamp properties.
        var options = new BiomeOptions<string>()
            .Default<Row, string?>(r => r.Value, (_, _) => "global")
            .Default<Row, int>(r => r.Number, (_, _) => 9)
            .Default<Row, bool>(r => r.Enabled, (_, _) => true)
            .Default<Row, DateTimeOffset>(r => r.CreatedAt, (_, ctx) => ctx.Now);
        var blueprint = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.Value, (_, _) => "specific");

        // Act: Compare blueprint-derived, globally defaulted, and explicitly customized rows.
        await Biome
            .Start("seed", options)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(blueprint, out var derived)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(Blueprint.For<string, Row>(_ => new()), out var global)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(
                blueprint,
                out var explicitRow,
                r =>
                {
                    r.Value = "";
                    r.Number = 5;
                }
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Specific derives win; global defaults fill sentinels while empty string and nonzero values survive.
        await Assert.That(derived.Value).IsEqualTo("specific");
        await Assert.That(global.Value).IsEqualTo("global");
        await Assert.That(global.Enabled).IsTrue();
        await Assert.That(global.CreatedAt).IsNotEqualTo(default(DateTimeOffset));
        await Assert.That(global.Number).IsEqualTo(9);
        await Assert.That(explicitRow.Value).IsEqualTo("");
        await Assert.That(explicitRow.Number).IsEqualTo(5);
    }

    [Test]
    public async Task Seed_created_and_recursive_aggregate_parts_are_deduplicated_by_reference()
    {
        // Shared rows in nested aggregates: expansion preserves insertion order and deduplicates by identity.
        // Arrange: Reference the same rows repeatedly through seed rows and nested aggregates.
        var shared = new Row();
        var other = new Row();
        var nested = new Aggregate([shared, other]);
        var outer = new Aggregate([nested, shared]);
        var store = new RecordingStore();
        var options = new BiomeOptions<string>().SeedEntities(_ => [shared, outer]);

        // Act: Add those same instances again as regular and aggregate outputs.
        await Biome
            .Start("seed", options, store)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(Blueprint.For<string, Row>(_ => shared), out _)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(Blueprint.For<string, Aggregate>(_ => outer), out _)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Expansion saves each distinct reference once, in first-seen order.
        await Assert.That(store.Entities.Count).IsEqualTo(2);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(shared);
        await Assert.That(store.Entities[1]).IsSameReferenceAs(other);
    }

    [Test]
    public async Task Blueprint_and_step_transients_exclude_nested_aggregate_parts()
    {
        // Nested previews: blueprint and step transience both suppress the entire aggregate branch.
        // Arrange: Prepare a nested aggregate blueprint and an empty recording store.
        var store = new RecordingStore();
        var blueprint = Blueprint.For<string, Aggregate>(_ =>
            new([new Row(), new Aggregate([new Row()])])
        );

        // Act: Exclude one output by variant and another by the latest-step Transient marker.
        await Biome
            .Start("seed", new BiomeOptions<string>(), store)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(blueprint.AsTransient(), out _)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(blueprint, out _)
            // Built and returned, but NOT saved; the latest step remains available to inspection.
            .Transient()
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Neither transient aggregate contributes any persistence rows.
        await Assert.That(store.Entities.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Time_precision_can_be_disabled_or_changed_and_provider_time_is_utc()
    {
        // Precision configuration: the same clock can produce exact ticks or millisecond-rounded setup.
        // Arrange: Share one sub-millisecond instant between two separately configured sessions.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var clock = new FakeTimeProvider(instant);
        var blueprint = Blueprint.For<string, Row>(ctx => new() { CreatedAt = ctx.Now });

        // Act: Build once with precision disabled and once with millisecond truncation.
        await Biome
            .Start("seed", new BiomeOptions<string>().TimePrecision(TimeSpan.Zero), time: clock)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(blueprint, out var exact)
            // Only this call saves; construction above was eager.
            .BuildAsync();
        await Biome
            .Start(
                "seed",
                new BiomeOptions<string>().TimePrecision(TimeSpan.FromMilliseconds(1)),
                time: clock
            )
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(blueprint, out var rounded)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Exact ticks survive the first session while the second truncates to its configured precision.
        await Assert.That(exact.CreatedAt).IsEqualTo(instant);
        await Assert.That(rounded.CreatedAt.Ticks % TimeSpan.TicksPerMillisecond).IsEqualTo(0L);
    }

    [Test]
    public async Task Build_twice_and_add_after_build_throw()
    {
        // Session lifetime: one save ends construction, and retrying BuildAsync is an error.
        // Arrange: Start an empty session so lifetime behavior is isolated from entity factories.
        var biome = Biome.Start("seed", new BiomeOptions<string>());

        // Act: Complete its first BuildAsync successfully.
        await biome.BuildAsync();

        // Assert: The session rejects both another save and subsequent construction.
        await Assert.ThrowsAsync<InvalidOperationException>(() => biome.BuildAsync());
        Assert.Throws<InvalidOperationException>(() =>
            biome.AddOne(Blueprint.For<string, Row>(_ => new()), out _)
        );
    }

    [Test]
    public async Task Singular_child_requires_exactly_one_parent()
    {
        // Fan-out safety: singular sugar rejects both empty and multiple-parent anchors.
        // Arrange: Create one multiple-parent anchor and one empty anchor.
        var many = TestBiome.Start(out _).AddCodeRepositories(out _, 2);
        var empty = TestBiome.Start(out _).AddCodeRepositories(out _, 0);

        // Act: Attempt singular child construction against both invalid anchors.

        // Assert: Both throw; use plural Add for fan-out instead of singular sugar.
        Assert.Throws<InvalidOperationException>(() => many.AddPullRequest(out _));
        Assert.Throws<InvalidOperationException>(() => empty.AddPullRequest(out _));
    }

    [Test]
    public async Task Missing_ancestor_error_names_the_type()
    {
        // Missing relationships: Ancestor fails clearly rather than silently inventing a parent graph.
        // Arrange: Use a root factory asking for a repository ancestor that was never supplied.
        var blueprint = Blueprint.For<string, Row>(ctx =>
        {
            ctx.Ancestor<CodeRepository>();
            return new();
        });
        // Act: Invoke that factory and capture its missing-ancestor exception.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Biome.Start("seed", new BiomeOptions<string>()).AddOne(blueprint, out _)
        );

        // Assert: The message names CodeRepository so the missing relationship is actionable.
        await Assert.That(exception.Message).Contains("CodeRepository");
    }

    [Test]
    public async Task Missing_inspection_and_invalid_property_and_count_throw_clear_errors()
    {
        // Invalid setup: missing inspection results, negative counts, nested/read-only properties, and precision fail clearly.
        // Arrange: Start an empty session and deliberately invalid configuration values.
        var biome = Biome.Start("seed", new BiomeOptions<string>());

        // Act: Exercise missing inspection, negative count/precision, and a nested read-only property.

        // Assert: Each invalid operation reports the corresponding setup error.
        Assert.Throws<InvalidOperationException>(() => biome.Last<Row>());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            biome.Add(Blueprint.For<string, Row>(_ => new()), out _, count: -1)
        );
        Assert.Throws<ArgumentException>(() =>
            Blueprint.For<string, Row>(_ => new()).Derive(r => r.Value!.Length, (_, _) => 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BiomeOptions<string>().TimePrecision(TimeSpan.FromTicks(-1))
        );
    }

    [Test]
    public async Task Context_next_progresses_and_cyclic_sequence_fails_instead_of_hanging()
    {
        // Direct sequence use: ctx.Next advances session state; cyclic exhausted progressions throw instead of hanging.
        // Arrange: Use ctx.Next directly in the factory, then define a separate cyclic sequence.
        var sequence = Sequence.Of(1, n => n + 1);
        var blueprint = Blueprint.For<string, Row>(ctx => new() { Number = ctx.Next(sequence) });

        // Act: Build two rows from the progressing sequence.
        await Biome
            .Start("seed", new BiomeOptions<string>())
            // ctx.Next advances in each factory; use sequence Derive when explicit-value skipping is required.
            .Add(blueprint, out var rows, count: 2)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Progression reaches 2; an exhausted constant sequence fails rather than looping forever.
        await Assert.That(rows[1].Number).IsEqualTo(2);

        var cycle = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.Number, Sequence.Of(1, _ => 1));
        Assert.Throws<InvalidOperationException>(() =>
            Biome.Start("seed", new BiomeOptions<string>()).Add(cycle, out _, count: 2)
        );
    }

    [Test]
    public async Task Parent_scope_requires_parent_and_aggregate_cycles_throw()
    {
        // Invalid graph shape: parent-scoped sequences need a parent, and cyclic aggregates cannot be expanded.
        // Arrange: Prepare a root factory with PerParent and an aggregate that contains itself.
        var blueprint = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.Number, Sequence.Of(1, n => n + 1).PerParent());

        // Act: Attempt root assignment and recursive aggregate persistence.

        // Assert: Both invalid graph shapes throw clearly instead of guessing a parent or recursing forever.
        Assert.Throws<InvalidOperationException>(() =>
            Biome.Start("seed", new BiomeOptions<string>()).AddOne(blueprint, out _)
        );
        var cycle = new Aggregate([]);
        cycle.Parts.Add(cycle);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Biome.Start("seed", new BiomeOptions<string>().SeedEntities(_ => [cycle])).BuildAsync()
        );
    }

    [Test]
    public async Task Cancellation_is_forwarded_to_hooks_and_store_and_failed_save_skips_after_hooks()
    {
        // Save failure behavior: cancellation reaches hooks/store, and a failed save never runs after-save hooks.
        // Arrange: Capture tokens in both hooks and the store; prepare a deliberately failing store.
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var beforeToken = default(CancellationToken);
        var afterToken = default(CancellationToken);
        var store = new RecordingStore();
        var options = new BiomeOptions<string>()
            .BeforeSave(
                (_, ct) =>
                {
                    beforeToken = ct;
                    return ValueTask.CompletedTask;
                }
            )
            .AfterSave(
                (_, ct) =>
                {
                    afterToken = ct;
                    return ValueTask.CompletedTask;
                }
            );

        // Act: Save successfully with a token, then exercise failure and cancellation paths.
        await Biome.Start("seed", options, store).BuildAsync(token);

        // Assert: Tokens propagate unchanged; failure skips after hooks and a session cannot retry its save.
        await Assert.That(beforeToken).IsEqualTo(token);
        await Assert.That(store.Token).IsEqualTo(token);
        await Assert.That(afterToken).IsEqualTo(token);

        var after = false;
        var failureOptions = new BiomeOptions<string>().AfterSave(
            (_, _) =>
            {
                after = true;
                return ValueTask.CompletedTask;
            }
        );
        var failed = Biome.Start("seed", failureOptions, new FailingStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failed.BuildAsync());
        await Assert.That(after).IsFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(() => failed.BuildAsync());
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Biome.Start("seed", new BiomeOptions<string>()).BuildAsync(token)
        );
    }

    [Test]
    public async Task Shared_sequence_reserves_explicit_values_from_all_properties_before_filling()
    {
        // One sequence on two properties: reserve every explicit value in the whole step before filling either property.
        // Arrange: Use the same sequence definition on two distinct properties.
        var sequence = Sequence.Of(1, n => n + 1);
        var blueprint = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.Number, sequence)
            .Derive(r => r.OtherNumber, sequence);

        // Act: Set the second property of the later entity explicitly to 1.
        await Biome
            .Start("seed", new BiomeOptions<string>())
            // Build the requested outputs eagerly; sequence reservation covers the entire step.
            .Add(blueprint, out var rows, _ => { }, r => r.OtherNumber = 1)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Every sequence property skips that reservation before any generated assignments occur.
        await Assert.That(rows[0].Number).IsEqualTo(2);
        await Assert.That(rows[0].OtherNumber).IsEqualTo(3);
        await Assert.That(rows[1].Number).IsEqualTo(4);
        await Assert.That(rows[1].OtherNumber).IsEqualTo(1);
    }

    [Test]
    public async Task Derives_run_in_declaration_order_per_entity_before_recording()
    {
        // Dependent derives: each entity finishes its derives before recording, so later entities can inspect completed outputs.
        // Arrange: Log derive order and inspection counts with a second derive dependent on the first.
        var events = new List<string>();
        var blueprint = Blueprint
            .For<string, Row>(_ => new())
            .Derive(
                r => r.Number,
                (_, ctx) =>
                {
                    events.Add($"first:{ctx.Index}:{ctx.All<Row>().Count}");
                    return ctx.Index + 1;
                }
            )
            .Derive(
                r => r.OtherNumber,
                (r, ctx) =>
                {
                    events.Add($"second:{ctx.Index}");
                    return r.Number + 10;
                }
            );

        // Act: Build two rows in one step.
        await Biome
            .Start("seed", new BiomeOptions<string>())
            // Build the requested outputs eagerly; sequence reservation covers the entire step.
            .Add(blueprint, out var rows, count: 2)
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Each row finishes in declaration order; the next row sees the previous completed output.
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo("first:0:0,second:0,first:1:1,second:1");
        await Assert.That(rows[1].OtherNumber).IsEqualTo(12);
    }

    private sealed class Aggregate(IEnumerable<object> parts) : IBiomeAggregate
    {
        internal List<object> Parts { get; } = [.. parts];

        public IEnumerable<object> GetEntities() => Parts;
    }

    private sealed class FailingStore : IBiomeStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<object> entities,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException("Save failed");
    }
}
