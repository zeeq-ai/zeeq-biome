using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.Regression;

public sealed class InputBlueprintTests
{
    private sealed class Input
    {
        public string? Id { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Kind { get; set; } = "invitation";
        public int Number { get; set; }
        public string? Label { get; set; }
    }

    private sealed class Invitation
    {
        public required string Id { get; init; }
        public required DateTimeOffset Created { get; init; }
        public required string Kind { get; init; }
        public string? Label { get; set; }
    }

    private sealed record Organization(string Name);

    private sealed record Repository(Organization Organization, string Name);

    private sealed record PullRequest(Repository Repository, int Number, string Label);

    private sealed record Lookup(PullRequest PullRequest, Repository Repository);

    private static InputBlueprint<string, Input, Invitation> Invitations() =>
        Blueprint
            .FromInput<string, Input, Invitation>(
                () => new(),
                (input, _) =>
                    new()
                    {
                        Id = input.Id!,
                        Created = input.Created,
                        Kind = input.Kind,
                    }
            )
            .Derive(i => i.Id, (_, ctx) => ctx.NewId("invite"))
            .Derive(i => i.Created, (_, ctx) => ctx.Now);

    private static ChildInputBlueprint<string, Repository, Input, PullRequest> PullRequests(
        Sequence<int> sequence
    ) =>
        Blueprint
            .ChildFromInput<string, Repository, Input, PullRequest>(
                () => new(),
                (parent, input, ctx) =>
                {
                    if (!ReferenceEquals(parent, ctx.Parent))
                    {
                        throw new InvalidOperationException("Typed parent differs from context.");
                    }
                    return new(parent, input.Number, input.Label!);
                }
            )
            .With(i => i.Kind = "pull-request")
            .Derive(i => i.Number, sequence)
            .Derive(i => i.Label, (i, ctx) => $"{ctx.Ancestor<Organization>().Name}/{i.Number}");

    [Test]
    public async Task Immutable_invitation_choices_are_customized_before_initialization_in_all_root_shapes()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var store = new RecordingStore();
        var options = new BiomeOptions<string>()
            .Ids(prefix => $"{prefix}-id")
            .Default<Invitation, string?>(i => i.Label, (_, _) => "output default");
        var root = Biome.Start("seed", options, store, new FakeTimeProvider(now));
        var bp = Invitations().With(i => i.Kind = "alias");
        var first = root.Add(bp, out var defaults);
        first.Add(bp, out var counted, 2);
        first.Add(bp, out var plain, i => i.Id = "explicit", i => i.Kind = "lookup");
        first.Add(bp, out var contexts, (i, ctx) => i.Id = $"context-{ctx.Index}");
        first.AddOne(bp, out var single);
        first.AddOne(bp, out var customized, i => i.Created = now.AddDays(-1));
        first.AddOne(
            bp,
            out var contextual,
            (i, ctx) => i.Created = ctx.Ago(TimeSpan.FromHours(1))
        );
        // A root input remains a root even when called from a child-capable anchor.
        first.Under(single).AddOne(bp, out var afterUnder);
        await Assert.That(first.Anchor[0]).IsSameReferenceAs(defaults[0]);
        await Assert.That(counted.Length).IsEqualTo(2);
        await Assert.That(plain[0].Id).IsEqualTo("explicit");
        await Assert.That(plain[1].Kind).IsEqualTo("lookup");
        await Assert.That(contexts[0].Id).IsEqualTo("context-0");
        await Assert.That(single.Kind).IsEqualTo("alias");
        await Assert.That(single.Created).IsEqualTo(now);
        await Assert.That(customized.Created).IsEqualTo(now.AddDays(-1));
        await Assert.That(contextual.Created).IsEqualTo(now.AddHours(-1));
        await Assert.That(afterUnder.Label).IsEqualTo("output default");
        await Assert.That(root.All<Input>().Count).IsEqualTo(0);
        Assert.Throws<ArgumentException>(() => root.Under(new Input()));
        await Assert.That(store.Calls).IsEqualTo(0);
        await root.BuildAsync();
        await Assert.That(store.Calls).IsEqualTo(1);
        await Assert.That(store.Entities.Count).IsEqualTo(10);
        await Assert.That(store.Entities.OfType<Input>().Count()).IsEqualTo(0);
        await Assert.That(root.All<Invitation>().Count).IsEqualTo(10);
        await Assert.That(root.Last<Invitation>()).IsSameReferenceAs(afterUnder);
        await Assert.That(() => root.BuildAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Typed_child_inputs_fan_out_reserve_per_repository_and_continue_with_output_ancestry()
    {
        var root = Biome.Start("seed", new BiomeOptions<string>());
        var repos = root.AddOne(Blueprint.For<string, Organization>(_ => new("zeeq")), out var org)
            .Add(
                Blueprint.ChildOf<string, Organization, Repository>(
                    (p, ctx) => new(p, $"repo-{ctx.Index}")
                ),
                out var repositories,
                2
            );
        var bp = PullRequests(Sequence.Of(1, n => n + 1).Per<Repository>());
        var prs = repos.Add(
            bp,
            out var requests,
            (i, ctx) => i.Label = $"{ctx.Parent.Name}:{ctx.ParentIndex}:{ctx.Index}",
            (i, _) => i.Number = 1
        );
        await Assert.That(string.Join(",", requests.Select(p => p.Number))).IsEqualTo("2,1,2,1");
        await Assert.That(requests[0].Label).IsEqualTo("repo-0:0:0");
        await Assert.That(requests[2].Label).IsEqualTo("repo-1:1:0");
        await Assert.That(requests[2].Repository).IsSameReferenceAs(repositories[1]);
        prs.Add(
            Blueprint.ChildOf<string, PullRequest, Lookup>(
                (p, ctx) => new(p, ctx.Ancestor<Repository>())
            ),
            out var lookups
        );
        await Assert.That(lookups[3].Repository).IsSameReferenceAs(repositories[1]);
        await Assert.That(lookups[3].PullRequest).IsSameReferenceAs(requests[3]);
        repos.Under(repositories[0]).AddOne(bp, out var sibling);
        await Assert.That(sibling.Number).IsEqualTo(3);
        await Assert.That(sibling.Label).IsEqualTo($"{org.Name}/3");
        repos.Under(repositories[0]).AddOne(bp, out var duplicate, i => i.Number = 1);
        repos
            .Under(repositories[0])
            .AddOne(bp, out var contextual, (i, ctx) => i.Label = ctx.Parent.Name);
        await Assert.That(duplicate.Number).IsEqualTo(1);
        await Assert.That(contextual.Number).IsEqualTo(4);
        await Assert.That(contextual.Label).IsEqualTo("repo-0");
        repos.Under(repositories[1]).Add(bp, out var counted, 2);
        repos.Under(repositories[1]).Add(bp, out var plain, i => i.Number = 9, i => i.Number = 9);
        repos.Under(repositories[1]).Add(bp, out var defaultChild);
        await Assert.That(string.Join(",", counted.Select(p => p.Number))).IsEqualTo("3,4");
        await Assert.That(string.Join(",", plain.Select(p => p.Number))).IsEqualTo("9,9");
        await Assert.That(defaultChild[0].Number).IsEqualTo(5);
        await Assert.That(root.All<Input>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Child_preflight_errors_leave_session_usable_and_factories_unexecuted()
    {
        var calls = 0;
        var root = Biome.Start("seed", new BiomeOptions<string>());
        var parents = root.Add(
            Blueprint.For<string, Repository>(_ => new(new("org"), "repo")),
            out var repositories,
            2
        );
        var bp = Blueprint.ChildFromInput<string, Repository, Input, PullRequest>(
            () =>
            {
                calls++;
                return new();
            },
            (p, i, _) => new(p, i.Number, "PR")
        );
        Assert.Throws<InvalidOperationException>(() => parents.AddOne(bp, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => parents.Add(bp, out _, -1));
        Assert.Throws<ArgumentNullException>(() => parents.Add(bp, out _, (Action<Input>[])null!));
        Assert.Throws<ArgumentException>(() =>
            parents.Add(bp, out _, new Action<Input>[] { null! })
        );
        Assert.Throws<ArgumentNullException>(() =>
            parents.AddOne(bp, out _, (Action<Input, ChildContext<string, Repository>>)null!)
        );
        // Anchor arrays are public: validate again immediately before construction.
        parents.Anchor[1] = new(new("foreign"), "foreign");
        Assert.Throws<ArgumentException>(() => parents.Add(bp, out _, 0));
        parents.Anchor[1] = null!;
        Assert.Throws<ArgumentException>(() => parents.Add(bp, out _));
        parents.Anchor[1] = repositories[0];
        await Assert.That(calls).IsEqualTo(0);
        parents.Add(bp, out var empty, 0);
        await Assert.That(empty.Length).IsEqualTo(0);
        parents.Under(repositories[0]).AddOne(bp, out _);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("input")]
    [Arguments("factory")]
    [Arguments("customizer")]
    [Arguments("derive")]
    public async Task Child_construction_failure_faults_session_and_prevents_partial_save(
        string phase
    )
    {
        var store = new RecordingStore();
        var root = Biome.Start("seed", new BiomeOptions<string>(), store);
        var parents = root.AddOne(
            Blueprint.For<string, Repository>(_ => new(new("org"), "repo")),
            out _
        );
        var bp = Blueprint.ChildFromInput<string, Repository, Input, PullRequest>(
            () => phase == "input" ? null! : new(),
            (p, i, _) => phase == "factory" ? null! : new(p, i.Number, "PR")
        );
        if (phase == "derive")
        {
            bp = bp.Derive(
                i => i.Number,
                (_, _) => throw new InvalidOperationException("derive failed")
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            parents.Add(
                bp,
                out _,
                i =>
                {
                    if (phase == "customizer")
                        throw new InvalidOperationException("customizer failed");
                }
            )
        );
        Assert.Throws<InvalidOperationException>(() => root.AddOne(Invitations(), out _));
        await Assert.That(() => root.BuildAsync()).Throws<InvalidOperationException>();
        await Assert.That(store.Calls).IsEqualTo(0);
        await Assert.That(root.All<PullRequest>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Ordinary_input_outputs_keep_seed_identity_dedup_and_latest_step_transience()
    {
        var seed = new Invitation
        {
            Id = "seed",
            Created = DateTimeOffset.UnixEpoch,
            Kind = "alias",
        };
        var store = new RecordingStore();
        var root = Biome.Start("seed", new BiomeOptions<string>().SeedEntities(_ => [seed]), store);
        var shared = Blueprint.FromInput<string, Input, Invitation>(() => new(), (_, _) => seed);
        var earlier = root.Add(shared, out var repeated, 2);
        root.AddOne(Invitations(), out var latest);
        earlier.Transient();
        await root.BuildAsync();
        await Assert.That(repeated[0]).IsSameReferenceAs(seed);
        await Assert.That(root.All<Invitation>().Count).IsEqualTo(3);
        await Assert.That(store.Entities.Count).IsEqualTo(1);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(seed);
        await Assert.That(root.Last<Invitation>()).IsSameReferenceAs(latest);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Input_callbacks_are_buffered_until_output_defaults_record_in_order(bool child)
    {
        var events = new List<string>();
        void Observe(string phase, BiomeContext<string> ctx) =>
            events.Add($"{phase}:{ctx.Index}:{ctx.All<Invitation>().Count}");
        var options = new BiomeOptions<string>().Default<Invitation, string?>(
            i => i.Label,
            (_, ctx) =>
            {
                Observe("default", ctx);
                return "label";
            }
        );
        var root = Biome.Start("seed", options);
        root.AddOne(Invitations(), out _);
        var sequence = Sequence.Of(1, n => n + 1);
        Invitation Build(Input input, BiomeContext<string> ctx)
        {
            Observe("factory", ctx);
            return new()
            {
                Id = input.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Created = ctx.Now,
                Kind = input.Kind,
            };
        }
        Invitation[] outputs;
        if (child)
        {
            var parents = root.AddOne(
                Blueprint.For<string, Repository>(_ => new(new("org"), "repo")),
                out _
            );
            var bp = Blueprint
                .ChildFromInput<string, Repository, Input, Invitation>(
                    () => new(),
                    (p, i, ctx) => Build(i, ctx)
                )
                .With((_, ctx) => Observe("trait", ctx))
                .Derive(i => i.Number, sequence)
                .Derive(
                    i => i.Label,
                    (_, ctx) =>
                    {
                        Observe("derive", ctx);
                        return "input label";
                    }
                );
            parents.Add(
                bp,
                out outputs,
                (_, ctx) => Observe("customizer", ctx),
                (i, ctx) =>
                {
                    i.Number = 1;
                    Observe("customizer", ctx);
                }
            );
        }
        else
        {
            var bp = Blueprint
                .FromInput<string, Input, Invitation>(() => new(), Build)
                .With((_, ctx) => Observe("trait", ctx))
                .Derive(i => i.Number, sequence)
                .Derive(
                    i => i.Label,
                    (_, ctx) =>
                    {
                        Observe("derive", ctx);
                        return "input label";
                    }
                );
            root.Add(
                bp,
                out outputs,
                (_, ctx) => Observe("customizer", ctx),
                (i, ctx) =>
                {
                    i.Number = 1;
                    Observe("customizer", ctx);
                }
            );
        }
        await Assert
            .That(string.Join(",", events))
            .IsEqualTo(
                "default:0:0,trait:0:1,customizer:0:1,trait:1:1,customizer:1:1,derive:0:1,factory:0:1,derive:1:1,factory:1:1,default:0:1,default:1:2"
            );
        await Assert.That(outputs[0].Id).IsEqualTo("2");
        await Assert.That(outputs[1].Id).IsEqualTo("1");
        await Assert.That(root.All<Input>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Child_input_aggregate_parts_keep_persistence_and_ownership_rules()
    {
        var store = new RecordingStore();
        var root = Biome.Start("seed", new BiomeOptions<string>(), store);
        var parents = root.AddOne(
            Blueprint.For<string, Repository>(_ => new(new("org"), "repo")),
            out _
        );
        var bp = Blueprint.ChildFromInput<string, Repository, Input, InvitationGroup>(
            () => new(),
            (_, input, ctx) =>
                new(
                    new Invitation
                    {
                        Id = input.Id ?? ctx.NewId("invite"),
                        Created = ctx.Now,
                        Kind = input.Kind,
                    }
                )
        );
        var group = parents.AddOne(bp, out var output);
        Assert.Throws<ArgumentException>(() => root.Under(output.Invitation));
        group.AddOne(
            Blueprint.ChildOf<string, InvitationGroup, Lookup>(
                (_, ctx) =>
                    new(new(ctx.Ancestor<Repository>(), 1, "lookup"), ctx.Ancestor<Repository>())
            ),
            out var lookup
        );
        parents.AddOne(bp.AsTransient(), out var transient);
        await root.BuildAsync();
        await Assert.That(root.All<InvitationGroup>().Count).IsEqualTo(2);
        await Assert.That(root.All<Invitation>().Count).IsEqualTo(0);
        await Assert.That(store.Entities.Contains(output.Invitation)).IsTrue();
        await Assert.That(store.Entities.Contains(transient.Invitation)).IsFalse();
        await Assert.That(store.Entities.Contains(output)).IsFalse();
        await Assert.That(store.Entities.Contains(lookup)).IsTrue();
    }

    private sealed record InvitationGroup(Invitation Invitation) : IBiomeAggregate
    {
        public IEnumerable<object> GetEntities() => [Invitation];
    }
}
