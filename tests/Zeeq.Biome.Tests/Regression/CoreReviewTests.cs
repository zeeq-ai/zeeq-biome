using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Regression;

public sealed class CoreReviewTests
{
    // Records deliberately compare equal by value: graph ownership and scopes must still use identity.
    private sealed record Parent(string Name);

    private sealed class Row
    {
        public int Number { get; set; }
        public int OtherNumber { get; set; }
        public int? NullableNumber { get; set; }
        public string? Label { get; set; }
        public string? Tag { get; set; }
    }

    private sealed class Leaf
    {
        public Parent Ancestor { get; init; } = null!;
    }

    private sealed class Input
    {
        public int Number { get; set; }
        public string? Label { get; set; }
    }

    private sealed class Aggregate(IEnumerable<object> parts) : IBiomeAggregate
    {
        public int Number { get; init; }
        public string? Tag { get; set; }

        public IEnumerable<object> GetEntities() => parts;
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Transient_parts_win_over_overlapping_inclusions_in_both_orders(
        bool transientFirst,
        bool blueprintTransient
    )
    {
        // A preview aggregate shares rows with the seed, another aggregate, and a separate Add.
        // Arrange: both collection orders and both ways of declaring transience obey identity exclusion.
        var shared = new Row();
        var previewOnly = new Row();
        var kept = new Row();
        var preview = new Aggregate([new Aggregate([shared]), previewOnly]);
        var included = new Aggregate([shared, kept]);
        var store = new RecordingStore();
        var biome = Biome.Start(
            "seed",
            new BiomeOptions<string>().SeedEntities(_ => [shared]),
            store
        );
        var previewBlueprint = Blueprint.For<string, Aggregate>(_ => preview);
        void IncludeRows()
        {
            // Overlapping references must not defeat an exclusion, even when collected first.
            biome
                .AddOne(Blueprint.For<string, Aggregate>(_ => included), out _)
                .AddOne(Blueprint.For<string, Row>(_ => shared), out _);
        }
        void PreviewRows()
        {
            var added = biome.AddOne(
                blueprintTransient ? previewBlueprint.AsTransient() : previewBlueprint,
                out _
            );
            if (!blueprintTransient)
            {
                added.Transient();
            }
        }

        // Act: expansion computes every excluded part before filtering the combined collection.
        if (transientFirst)
        {
            PreviewRows();
            IncludeRows();
        }
        else
        {
            IncludeRows();
            PreviewRows();
        }
        await biome.BuildAsync();

        // Assert: transient exclusion wins even over the earliest seed inclusion.
        await Assert.That(store.Entities.Count).IsEqualTo(1);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(kept);
        await Assert.That(biome.All<Aggregate>().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Conflicting_child_identity_is_rejected_before_step_recording()
    {
        // A buggy child factory returns one row under two distinct parents in one fan-out step.
        // Arrange: create owned parents so the failure isolates ancestry, not ownership validation.
        var shared = new Row();
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        var parents = biome.Add(Blueprint.For<string, Parent>(_ => new("parent")), out _, count: 2);
        var child = Blueprint.ChildOf<string, Parent, Row>((_, _) => shared);

        // Act: completing the buffered step must validate every parent link before recording its first row.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            parents.Add(child, out _, count: 1)
        );

        // Assert: the error names entity/parent types; no arbitrary last parent wins.
        await Assert.That(exception.Message).Contains(nameof(Row));
        await Assert.That(exception.Message).Contains(nameof(Parent));
        await Assert.That(exception.Message).Contains("parent references conflict");
        await Assert.That(biome.All<Row>().Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Previously_parented_identity_cannot_be_readded_as_root_or_reparented(
        bool asRoot
    )
    {
        // Existing children cannot silently move to another branch, including the root branch.
        // Arrange: record one child beneath the first of two owned parents.
        var shared = new Row();
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        biome.Add(Blueprint.For<string, Parent>(_ => new("parent")), out var parents, count: 2);
        var child = Blueprint.ChildOf<string, Parent, Row>((_, _) => shared);
        biome.Under(parents[0]).AddOne(child, out _);

        // Act: return that same identity through an incompatible root or child blueprint.
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            if (asRoot)
            {
                biome.AddOne(Blueprint.For<string, Row>(_ => shared), out _);
            }
            else
            {
                biome.Under(parents[1]).AddOne(child, out _);
            }
        });

        // Assert: ancestry stays fixed; the failed step did not add another recorded row.
        await Assert.That(exception.Message).Contains(nameof(Row));
        await Assert.That(exception.Message).Contains(nameof(Parent));
        await Assert
            .That(exception.Message)
            .Contains(asRoot ? "root (no parent)" : "parent references conflict");
        await Assert.That(biome.All<Row>().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Reusing_child_identity_under_same_parent_preserves_ancestor_lookup()
    {
        // Deliberate same-parent reuse is supported; saving still deduplicates by identity.
        // Arrange: return one shared child twice, then inspect its ancestry from a grandchild factory.
        var shared = new Row();
        var store = new RecordingStore();
        var biome = Biome.Start("seed", new BiomeOptions<string>(), store);
        var child = Blueprint.ChildOf<string, Parent, Row>((_, _) => shared);
        var leaf = Blueprint.ChildOf<string, Row, Leaf>(
            (_, ctx) => new() { Ancestor = ctx.Ancestor<Parent>() }
        );

        // Act: Under(shared) selects one identity instead of fanning grandchildren over duplicate outputs.
        await biome
            .AddOne(Blueprint.For<string, Parent>(_ => new("owner")), out var parent)
            .Add(child, out _, count: 2)
            .Under(shared)
            .AddOne(leaf, out var created)
            .BuildAsync();

        // Assert: the recorded parent link is stable and only three distinct rows are saved.
        await Assert.That(created.Ancestor).IsSameReferenceAs(parent);
        await Assert.That(biome.All<Row>().Count).IsEqualTo(2);
        await Assert.That(store.Entities.Count).IsEqualTo(3);
    }

    [Test]
    [Arguments("factory")]
    [Arguments("trait")]
    [Arguments("customizer")]
    [Arguments("derive")]
    [Arguments("default")]
    [Arguments("child")]
    [Arguments("input")]
    public async Task Failed_Add_faults_session_and_blocks_stale_transience_and_save(string stage)
    {
        // A failed setup cannot save a partial step or accidentally mark the preceding successful step transient.
        // Arrange: the original exception must survive all later failures, including after sequence advancement.
        var original = new InvalidOperationException($"Broken {stage}");
        var sequence = Sequence.Of(1, n => n + 1);
        void Fail(string phase, BiomeContext<string> ctx)
        {
            if (stage == phase && ctx.Index == 1)
            {
                throw original;
            }
        }
        var options = new BiomeOptions<string>().Default<Row, string?>(
            r => r.Tag,
            (_, ctx) =>
            {
                Fail("default", ctx);
                return "default";
            }
        );
        var store = new RecordingStore();
        var biome = Biome.Start("seed", options, store);
        biome
            .AddOne(Blueprint.For<string, Row>(_ => new()), out var stable)
            .AddOne(Blueprint.For<string, Parent>(_ => new("parent")), out var parent);
        var blueprint = Blueprint
            .For<string, Row>(ctx =>
            {
                Fail("factory", ctx);
                return new();
            })
            .With((_, ctx) => Fail("trait", ctx))
            .Derive(r => r.Number, sequence)
            .Derive(
                r => r.Label,
                (_, ctx) =>
                {
                    Fail("derive", ctx);
                    return "derived";
                }
            );
        var input = Blueprint
            .FromInput<string, Input, Aggregate>(
                () => new(),
                (i, ctx) =>
                {
                    Fail("input", ctx);
                    return new([new Row { Number = i.Number }]);
                }
            )
            .Derive(i => i.Number, sequence);
        var child = Blueprint.ChildOf<string, Parent, Row>(
            (_, ctx) =>
            {
                Fail("child", ctx);
                return new();
            }
        );

        // Act: derives/defaults fail on the second row after the first row has been recorded.
        var thrown = Assert.Throws<InvalidOperationException>(() =>
        {
            if (stage == "input")
            {
                biome.Add(input, out _, count: 2);
            }
            else if (stage == "child")
            {
                biome.Under(parent).Add(child, out _, count: 2);
            }
            else
            {
                biome.Add(
                    blueprint,
                    out _,
                    (_, ctx) => Fail("customizer", ctx),
                    (_, ctx) => Fail("customizer", ctx)
                );
            }
        });
        var addFailure = Assert.Throws<InvalidOperationException>(() =>
            biome.AddOne(blueprint, out _)
        );
        var underFailure = Assert.Throws<InvalidOperationException>(() => biome.Under(stable));
        var transientFailure = Assert.Throws<InvalidOperationException>(() => biome.Transient());
        var saveFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            biome.BuildAsync()
        );

        // Assert: the initial throw is unchanged, and every later operation explains that earlier failure.
        await Assert.That(thrown).IsSameReferenceAs(original);
        foreach (var failure in new[] { addFailure, underFailure, transientFailure, saveFailure! })
        {
            await Assert.That(failure.Message).Contains("earlier Add failed");
            await Assert.That(failure.Message).Contains(original.Message);
            await Assert.That(failure.InnerException).IsSameReferenceAs(original);
        }
        await Assert.That(store.Calls).IsEqualTo(0);
        if (stage is "derive" or "default")
        {
            // Diagnostic inspection may show partial mutation; faulting prevents it reaching the store.
            await Assert.That(biome.All<Row>().Count).IsEqualTo(2);
            await Assert.That(biome.Last<Row>().Number).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Under_accepts_selected_seed_rows_and_rejects_untracked_aggregate_parts()
    {
        // Ownership is explicit: directly selected seed rows qualify, but aggregate expansion is persistence-only.
        // Arrange: evaluate SeedEntities once at Start so ownership and persistence use the same references.
        var selected = new Parent("selected");
        var seedPart = new Parent("seed part");
        var addedPart = new Parent("added part");
        var calls = 0;
        var options = new BiomeOptions<string>().SeedEntities(_ =>
        {
            calls++;
            return [selected, new Aggregate([seedPart])];
        });
        var biome = Biome.Start("seed", options);
        var child = Blueprint.ChildOf<string, Parent, Row>((_, _) => new());

        // Act: seed row navigation succeeds; neither seed nor created aggregate parts are automatically tracked.
        biome.Under(selected).AddOne(child, out _);
        var seedError = Assert.Throws<ArgumentException>(() => biome.Under(seedPart));
        biome.AddOne(Blueprint.For<string, Aggregate>(_ => new([addedPart])), out _);
        var partError = Assert.Throws<ArgumentException>(() => biome.Under(addedPart));
        // A separate Add makes that part explicitly owned, so it can now anchor children.
        await biome
            .AddOne(Blueprint.For<string, Parent>(_ => addedPart), out _)
            .Under(addedPart)
            .AddOne(child, out _)
            .BuildAsync();

        // Assert: rejected navigation is preflight validation, so the corrected scenario can still complete.
        await Assert.That(seedError.Message).Contains("untracked aggregate parts");
        await Assert.That(partError.Message).Contains("not owned");
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(biome.All<Row>().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Under_rejects_foreign_and_null_entries_with_the_offending_index()
    {
        // Two sessions create value-equal parents; reference identity still decides graph ownership.
        // Arrange: the rejected array must identify its bad element before any child factory runs.
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        biome.AddOne(Blueprint.For<string, Parent>(_ => new("same")), out var owned);
        Biome
            .Start("seed", new BiomeOptions<string>())
            .AddOne(Blueprint.For<string, Parent>(_ => new("same")), out var foreign);

        // Act: neither another session's output nor a null array entry can become this session's parent.
        var foreignError = Assert.Throws<ArgumentException>(() =>
            biome.Under(new[] { owned, foreign })
        );
        var nullError = Assert.Throws<ArgumentException>(() => biome.Under(new[] { owned, null! }));
        await biome.BuildAsync();

        // Assert: value equality is insufficient, and both errors name index 1.
        await Assert.That(owned).IsEqualTo(foreign);
        await Assert.That(foreignError.Message).Contains("index 1");
        await Assert.That(foreignError.Message).Contains("not owned");
        await Assert.That(nullError.Message).Contains("index 1");
        await Assert.That(nullError.Message).Contains("null");
    }

    [Test]
    public async Task Under_copies_parent_arrays_and_child_add_revalidates_mutated_output_anchors()
    {
        // Caller-owned output arrays are mutable; a changed array must not smuggle a foreign parent into a step.
        // Arrange: Under copies valid references, while the original Add builder still exposes its output array.
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        var original = biome.AddOne(
            Blueprint.For<string, Parent>(_ => new("owned")),
            out var owned
        );
        var copied = biome.Under(original.Anchor);
        var child = Blueprint.ChildOf<string, Parent, Row>((_, _) => new());

        // Act: mutate the original output; copied navigation stays safe, original child construction is rejected.
        original.Anchor[0] = new("foreign");
        copied.AddOne(child, out _);
        var error = Assert.Throws<ArgumentException>(() => original.AddOne(child, out _));
        await biome.BuildAsync();

        // Assert: the safe branch used its original identity and no foreign child was recorded.
        await Assert.That(copied.Anchor[0]).IsSameReferenceAs(owned);
        await Assert.That(error.Message).Contains("index 0");
        await Assert.That(biome.All<Row>().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Next_only_skips_prior_reservations_while_Derive_reserves_the_whole_step()
    {
        // ctx.Next is immediate; sequence Derive is the fill-only API for whole-step explicit reservation.
        // Arrange: use the same definition in independent sessions to compare both assignment patterns.
        var sequence = Sequence.Of(1, n => n + 1);
        var immediate = Blueprint.For<string, Row>(ctx => new() { Number = ctx.Next(sequence) });
        var reserved = Blueprint.For<string, Row>(_ => new()).Derive(r => r.Number, sequence);
        var options = new BiomeOptions<string>();

        // Act: the second customizer explicitly chooses 1 after the first factory has already consumed it.
        await Biome
            .Start("seed", options)
            .Add(immediate, out var fromNext, _ => { }, r => r.Number = 1)
            .BuildAsync();
        await Biome
            .Start("seed", options)
            .Add(reserved, out var fromDerive, _ => { }, r => r.Number = 1)
            .BuildAsync();
        // Next does skip an explicit value reserved by a completed earlier step.
        await Biome
            .Start("seed", options)
            .AddOne(reserved, out _, r => r.Number = 1)
            .AddOne(immediate, out var afterReservation)
            .BuildAsync();

        // Assert: Next cannot predict future overrides; Derive reserves 1 first and fills the unset row with 2.
        await Assert.That(fromNext[0].Number).IsEqualTo(1);
        await Assert.That(fromNext[1].Number).IsEqualTo(1);
        await Assert.That(fromDerive[0].Number).IsEqualTo(2);
        await Assert.That(fromDerive[1].Number).IsEqualTo(1);
        await Assert.That(afterReservation.Number).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Null_customizer_elements_are_rejected_before_root_child_or_input_factories(
        bool contextAware
    )
    {
        // Null delegates are argument errors, not mid-step null dereferences that corrupt the scenario.
        // Arrange: counters distinguish validation from attempted construction in all three blueprint families.
        var roots = 0;
        var children = 0;
        var inputs = 0;
        var aggregates = 0;
        var root = Blueprint.For<string, Row>(_ =>
        {
            roots++;
            return new();
        });
        var child = Blueprint.ChildOf<string, Parent, Row>(
            (_, _) =>
            {
                children++;
                return new();
            }
        );
        var input = Blueprint.FromInput<string, Input, Aggregate>(
            () =>
            {
                inputs++;
                return new();
            },
            (_, _) =>
            {
                aggregates++;
                return new([]);
            }
        );
        var biome = Biome.Start("seed", new BiomeOptions<string>());
        var parents = biome.AddOne(Blueprint.For<string, Parent>(_ => new("owned")), out _);

        // Act: the null is at index 1, after a valid delegate that must never be invoked.
        var rootError = Assert.Throws<ArgumentException>(() =>
        {
            if (contextAware)
            {
                biome.Add(
                    root,
                    out _,
                    new Action<Row, BiomeContext<string>>[] { (_, _) => { }, null! }
                );
            }
            else
            {
                biome.Add(root, out _, new Action<Row>[] { _ => { }, null! });
            }
        });
        var childError = Assert.Throws<ArgumentException>(() =>
        {
            if (contextAware)
            {
                parents.Add(
                    child,
                    out _,
                    new Action<Row, ChildContext<string, Parent>>[] { (_, _) => { }, null! }
                );
            }
            else
            {
                parents.Add(child, out _, new Action<Row>[] { _ => { }, null! });
            }
        });
        var inputError = Assert.Throws<ArgumentException>(() =>
        {
            if (contextAware)
            {
                biome.Add(
                    input,
                    out _,
                    new Action<Input, BiomeContext<string>>[] { (_, _) => { }, null! }
                );
            }
            else
            {
                biome.Add(input, out _, new Action<Input>[] { _ => { }, null! });
            }
        });
        await biome.BuildAsync();

        // Assert: every family reports the delegate index and leaves the session healthy.
        foreach (var error in new[] { rootError, childError, inputError })
        {
            await Assert.That(error.Message).Contains("index 1");
            await Assert.That(error.ParamName).IsEqualTo("customize");
        }
        await Assert.That(roots + children + inputs + aggregates).IsEqualTo(0);
        await Assert.That(biome.All<Row>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Nullable_null_fills_but_nullable_zero_survives_derives_and_defaults()
    {
        // default(int?) is null, not zero: explicit nullable zero is a meaningful call-site value.
        // Arrange: compare blueprint-specific derives with a global nullable default.
        var options = new BiomeOptions<string>().Default<Row, int?>(
            r => r.NullableNumber,
            (_, _) => 8
        );
        var derived = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.NullableNumber, (_, _) => 7);
        var global = Blueprint.For<string, Row>(_ => new());

        // Act: only null properties are eligible for filling at either pipeline stage.
        await Biome
            .Start("seed", options)
            .Add(derived, out var specific, _ => { }, r => r.NullableNumber = 0)
            .Add(global, out var defaults, _ => { }, r => r.NullableNumber = 0)
            .BuildAsync();

        // Assert: call site wins, nothing overwrites; the specific derive also beats the global fallback.
        await Assert.That(specific[0].NullableNumber).IsEqualTo(7);
        await Assert.That(specific[1].NullableNumber).IsEqualTo(0);
        await Assert.That(defaults[0].NullableNumber).IsEqualTo(8);
        await Assert.That(defaults[1].NullableNumber).IsEqualTo(0);
    }

    [Test]
    public async Task Identical_sequence_definitions_have_independent_instance_state()
    {
        // Two definitions with identical start/advance functions represent independent numbering streams.
        // Arrange: both are session-scoped, so their instance identity is the only distinction.
        var first = Sequence.Of(1, n => n + 1);
        var second = Sequence.Of(1, n => n + 1);
        var blueprint = Blueprint
            .For<string, Row>(_ => new())
            .Derive(r => r.Number, first)
            .Derive(r => r.OtherNumber, second);

        // Act: both properties independently progress under the same session scope.
        await Biome
            .Start("seed", new BiomeOptions<string>())
            .Add(blueprint, out var rows, count: 2)
            .BuildAsync();

        // Assert: sequence identity, not definition equality, keys progression.
        await Assert.That(rows[0].Number).IsEqualTo(1);
        await Assert.That(rows[0].OtherNumber).IsEqualTo(1);
        await Assert.That(rows[1].Number).IsEqualTo(2);
        await Assert.That(rows[1].OtherNumber).IsEqualTo(2);
    }

    [Test]
    public async Task Value_equal_parents_have_distinct_identity_scopes_and_Under_resumes_each()
    {
        // Value-equal records still represent different parents; neither local nor ancestor scope uses Equals.
        // Arrange: distinct parents intentionally share the same record value.
        var local = Sequence.Of(1, n => n + 1).PerParent();
        var ancestor = Sequence.Of(1, n => n + 1).Per<Parent>();
        var child = Blueprint
            .ChildOf<string, Parent, Row>((_, _) => new())
            .Derive(r => r.Number, local)
            .Derive(r => r.OtherNumber, ancestor);
        var biome = Biome.Start("seed", new BiomeOptions<string>());

        // Act: 2 parent identities × 2 rows = 4 rows; revisit only the first parent.
        await biome
            .Add(Blueprint.For<string, Parent>(_ => new("same")), out var parents, count: 2)
            .Add(child, out var rows, count: 2)
            .Under(parents[0])
            .AddOne(child, out var resumed)
            .BuildAsync();

        // Assert: each parent starts both streams at 1, while the revisited parent continues at 3.
        await Assert.That(parents[0]).IsEqualTo(parents[1]);
        await Assert.That(rows[0].Number).IsEqualTo(1);
        await Assert.That(rows[2].Number).IsEqualTo(1);
        await Assert.That(rows[2].OtherNumber).IsEqualTo(1);
        await Assert.That(resumed.Number).IsEqualTo(3);
        await Assert.That(resumed.OtherNumber).IsEqualTo(3);
    }

    [Test]
    public async Task Input_sequence_reserves_explicit_values_before_building_aggregate_outputs()
    {
        // Aggregate inputs receive the same fill-only reservation guarantees as entity blueprints.
        // Arrange: the aggregate factory copies final input values into its coordinated persistence row.
        var sequence = Sequence.Of(1, n => n + 1);
        var blueprint = Blueprint
            .FromInput<string, Input, Aggregate>(
                () => new(),
                (input, _) => new([new Row { Number = input.Number, Label = input.Label }])
            )
            .Derive(i => i.Number, sequence)
            .Derive(i => i.Label, (i, _) => $"input-{i.Number}");
        var biome = Biome.Start("seed", new BiomeOptions<string>());

        // Act: reserve explicit 1 even though it belongs to the later input; resume on the next Add.
        await biome
            .Add(
                blueprint,
                out var graphs,
                _ => { },
                i =>
                {
                    i.Number = 1;
                    i.Label = "custom";
                }
            )
            .AddOne(blueprint, out var next)
            .BuildAsync();

        // Assert: input derives precede aggregate factories, and custom input labels survive.
        var rows = graphs.Select(g => (Row)g.GetEntities().Single()).ToArray();
        await Assert.That(rows[0].Number).IsEqualTo(2);
        await Assert.That(rows[0].Label).IsEqualTo("input-2");
        await Assert.That(rows[1].Number).IsEqualTo(1);
        await Assert.That(rows[1].Label).IsEqualTo("custom");
        await Assert.That(((Row)next.GetEntities().Single()).Number).IsEqualTo(3);
    }

    [Test]
    public async Task Multiple_save_hooks_remove_reorder_and_append_rows_in_registration_order()
    {
        // Save hooks are an explicit escape hatch: they can remove/reorder rows, not only append them.
        // Arrange: two hooks on each side of the store expose their exact registration order.
        var baseline = new Row();
        var appended = new Row();
        var events = new List<string>();
        var store = new RecordingStore { OnSave = () => events.Add("save") };
        var options = new BiomeOptions<string>()
            .SeedEntities(_ => [baseline])
            .BeforeSave(
                (ctx, _) =>
                {
                    events.Add("before-1");
                    ctx.Entities.Remove(baseline);
                    ctx.Entities.Reverse();
                    return ValueTask.CompletedTask;
                }
            )
            .BeforeSave(
                (ctx, _) =>
                {
                    events.Add("before-2");
                    ctx.Entities.Add(appended);
                    return ValueTask.CompletedTask;
                }
            )
            .AfterSave(
                (_, _) =>
                {
                    events.Add("after-1");
                    return ValueTask.CompletedTask;
                }
            )
            .AfterSave(
                (_, _) =>
                {
                    events.Add("after-2");
                    return ValueTask.CompletedTask;
                }
            );

        // Act: collection order starts with the seed; the first hook deliberately replaces that order.
        await Biome
            .Start("seed", options, store)
            .Add(Blueprint.For<string, Row>(_ => new()), out var rows, count: 3)
            .BuildAsync();

        // Assert: the store receives the edited list once, between the before and after hook groups.
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo("before-1,before-2,save,after-1,after-2");
        await Assert.That(store.Entities.Count).IsEqualTo(4);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(rows[2]);
        await Assert.That(store.Entities[1]).IsSameReferenceAs(rows[1]);
        await Assert.That(store.Entities[2]).IsSameReferenceAs(rows[0]);
        await Assert.That(store.Entities[3]).IsSameReferenceAs(appended);
        await Assert.That(store.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Buffered_entity_factories_and_customizers_see_prior_steps_while_derives_see_recorded_rows()
    {
        // Inspection follows recording, not allocation: buffered factory/trait/customizer work cannot see its own step.
        // Arrange: a prior row makes Last visible; snapshots show when this step's outputs become visible.
        var events = new List<string>();
        void Observe(string phase, BiomeContext<string> ctx) =>
            events.Add($"{phase}:{ctx.Index}:{ctx.All<Row>().Count}:{ctx.Last<Row>().Number}");
        var options = new BiomeOptions<string>().Default<Row, string?>(
            r => r.Tag,
            (_, ctx) =>
            {
                Observe("default", ctx);
                return "tag";
            }
        );
        var biome = Biome.Start("seed", options);
        // Avoid observing the initial row before anything is recorded.
        biome.AddOne(Blueprint.For<string, Row>(_ => new() { Number = 99, Tag = "prior" }), out _);
        var blueprint = Blueprint
            .For<string, Row>(ctx =>
            {
                Observe("factory", ctx);
                return new() { Number = ctx.Index + 1 };
            })
            .With((_, ctx) => Observe("trait", ctx))
            .Derive(
                r => r.Label,
                (_, ctx) =>
                {
                    Observe("derive", ctx);
                    return "label";
                }
            );

        // Act: Index is only a cosmetic marker here; use sequences for real domain numbering.
        await biome
            .Add(
                blueprint,
                out _,
                (_, ctx) => Observe("customizer", ctx),
                (_, ctx) => Observe("customizer", ctx)
            )
            .BuildAsync();

        // Assert: the second derive/default sees the first completed row; no callback sees itself recorded.
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo(
                "factory:0:1:99,trait:0:1:99,customizer:0:1:99,factory:1:1:99,trait:1:1:99,customizer:1:1:99,derive:0:1:99,default:0:1:99,derive:1:2:1,default:1:2:1"
            );
        await Assert.That(biome.Last<Row>().Number).IsEqualTo(2);
    }

    [Test]
    public async Task Buffered_input_derives_and_aggregate_factories_see_prior_steps_until_output_recording()
    {
        // Inputs are not graph entities; every input derive and aggregate factory sees completed prior steps only.
        // Arrange: keep one aggregate visible, then track Last and All through a two-output input step.
        var events = new List<string>();
        void Observe(string phase, BiomeContext<string> ctx) =>
            events.Add(
                $"{phase}:{ctx.Index}:{ctx.All<Aggregate>().Count}:{ctx.Last<Aggregate>().Number}"
            );
        var options = new BiomeOptions<string>().Default<Aggregate, string?>(
            g => g.Tag,
            (_, ctx) =>
            {
                Observe("default", ctx);
                return "tag";
            }
        );
        var biome = Biome.Start("seed", options);
        biome.AddOne(
            Blueprint.For<string, Aggregate>(_ => new([]) { Number = 99, Tag = "prior" }),
            out _
        );
        var sequence = Sequence.Of(1, n => n + 1);
        var blueprint = Blueprint
            .FromInput<string, Input, Aggregate>(
                () => new(),
                (input, ctx) =>
                {
                    Observe("factory", ctx);
                    return new([]) { Number = input.Number };
                }
            )
            .With((_, ctx) => Observe("trait", ctx))
            .Derive(i => i.Number, sequence)
            .Derive(
                i => i.Label,
                (_, ctx) =>
                {
                    Observe("derive", ctx);
                    return "label";
                }
            );

        // Act: sequence reservation and input building finish before aggregate outputs enter recording/defaults.
        await biome
            .Add(
                blueprint,
                out _,
                (_, ctx) => Observe("customizer", ctx),
                (_, ctx) => Observe("customizer", ctx)
            )
            .BuildAsync();

        // Assert: only output defaults can observe earlier recorded outputs in the current step.
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo(
                "trait:0:1:99,customizer:0:1:99,trait:1:1:99,customizer:1:1:99,derive:0:1:99,factory:0:1:99,derive:1:1:99,factory:1:1:99,default:0:1:99,default:1:2:1"
            );
        await Assert.That(biome.Last<Aggregate>().Number).IsEqualTo(2);
        await Assert.That(biome.All<Input>().Count).IsEqualTo(0);
    }
}
