# Authoring reference

Copy-ready templates. Names follow the README example domain (`ZeeqSeed`, code reviews). Replace them with your app's types.

## Layout

```text
Tests.Common/Biome/
  ZeeqSeed.cs             seed type + Generate()
  TestBiome.cs            options + Start overloads (no-DB, DbContext)
  CodeReviews/
    Blueprints.cs         sequences, blueprints, variants (one file: init order)
    Traits.cs             PullRequests.Closed, …
    Sugar.cs              four one-liners per type + scenario helpers
  Membership/ …
```

## Seed and entry point

```csharp
public sealed record ZeeqSeed(User Owner, Organization Organization)
{
    public static ZeeqSeed Generate()
    {
        var owner = new User { Id = $"user_{Guid.CreateVersion7():N}", DisplayName = "Owner" };
        var organization = new Organization
        {
            Id = $"org_{Guid.CreateVersion7():N}",
            DisplayName = "Test Organization",
            CreatedByUserId = owner.Id,
        };
        return new(owner, organization);
    }
}

public static partial class TestBiome
{
    private static readonly BiomeOptions<ZeeqSeed> Options = new BiomeOptions<ZeeqSeed>()
        .SeedEntities(seed => [seed.Owner, seed.Organization]);

    public static BiomeBuilder<ZeeqSeed, ZeeqSeed> Start(
        out ZeeqSeed seed, IBiomeStore? store = null, TimeProvider? time = null)
    {
        seed = ZeeqSeed.Generate();
        return Biome.Start(seed, Options, store, time);
    }

    public static BiomeBuilder<ZeeqSeed, ZeeqSeed> Start(
        DbContext db, out ZeeqSeed seed, TimeProvider? time = null) =>
        Start(out seed, new EfCoreStore(db), time);
}
```

- The seed is created outside the session, so it can't use `ctx.Now`. If seed timestamps are compared against database values, round them yourself, or set them from the same `TimeProvider`.
- An existing fixture type can be the seed directly: `BiomeOptions<SeedContext>().SeedEntities(s => s.GetSeedEntities())`.
- `Biome.Start(seed, options, store, time)`: a `null` store means `NullStore`. A `null` time means a frozen clock. Precision rounding applies to both.

## Blueprints

```csharp
public static class Blueprints
{
    // Sequences first: fields initialize top to bottom.
    public static readonly Sequence<int> PullRequestNumbers =
        Sequence.Of(1, n => n + 1).Per<CodeRepository>();

    public static readonly Blueprint<ZeeqSeed, CodeRepository> Repository =
        Blueprint.For<ZeeqSeed, CodeRepository>(ctx => new()
        {
            Id = ctx.NewId("repo"),
            OrganizationId = ctx.Seed.Organization.Id,
            OwnerQualifiedName = "zeeq-ai/example",
            Enabled = true,
            CreatedAtUtc = ctx.Now,
        });

    public static readonly Blueprint<ZeeqSeed, CodeRepository> DisabledRepository =
        Repository.With(r => r.Enabled = false);                  // variant: below its base

    public static readonly ChildBlueprint<ZeeqSeed, CodeRepository, PullRequestRecord> PullRequest =
        Blueprint.ChildOf<ZeeqSeed, CodeRepository, PullRequestRecord>((repo, ctx) => new()
            {
                Id = ctx.NewId("pr"),
                OrganizationId = repo.OrganizationId,
                RepositoryId = repo.Id,
                CreatedAtUtc = ctx.Now,
                // PullRequestNumber, Title, Branch: left unset for the derives below
            })
            .Derive(pr => pr.PullRequestNumber, PullRequestNumbers)
            .Derive(pr => pr.Title, (pr, _) => $"Pull request {pr.PullRequestNumber}")
            .Derive(pr => pr.Branch, (pr, _) => $"feature/{pr.PullRequestNumber}")
            .Derive(pr => pr.GitHubNodeId, (_, ctx) => ctx.NewId("PR"));

    public static readonly ChildBlueprint<ZeeqSeed, PullRequestRecord, CodeReviewRecord> Review =
        Blueprint.ChildOf<ZeeqSeed, PullRequestRecord, CodeReviewRecord>((pr, ctx) => new()
        {
            Id = ctx.NewId("review"),
            OrganizationId = pr.OrganizationId,
            PullRequestRecordId = pr.Id,
            RepositoryId = ctx.Ancestor<CodeRepository>().Id,      // grandparent via parent links
            CreatedAtUtc = ctx.Now,
        });
}
```

Context members:

| Member | Use |
|---|---|
| `ctx.Seed` | baseline rows (org, owner) |
| `ctx.Now`, `ctx.Ago(t)`, `ctx.FromNow(t)` | the session clock, already rounded |
| `ctx.NewId("prefix")` | `prefix_<uuidv7>` unless options override it |
| `ctx.Index` | position in this Add (per parent); cosmetic only |
| `ctx.Next(seq)` | advance a sequence manually (prefer `Derive(prop, seq)`) |
| `ctx.Ancestor<T>()` | nearest ancestor of type `T`; throws a clear error if none |
| `ctx.All<T>()`, `ctx.Last<T>()` | inspection only; never for wiring |
| `ctx.Parent`, `ctx.ParentIndex` | child context only |

Variant and trait forms:

- `bp.With(Action<T>)` and `bp.With(Action<T, BiomeContext<TSeed>>)` for root blueprints.
- Child blueprints also accept `Action<T, ChildContext<TSeed, TParent>>`. A base-context trait (`Action<T, BiomeContext<TSeed>>`) is assignable to it, so you can write a trait once and use it for both.
- Traits from `With` run before call-site customizers, so the call site always wins.

## Sequences

| Declaration | Counter is per | State key |
|---|---|---|
| `Sequence.Of(1, n => n + 1)` / `.PerSession()` | test | (sequence, session) |
| `.PerParent()` | immediate parent | (sequence, parent reference) |
| `.Per<TAncestor>()` | nearest `TAncestor` | (sequence, ancestor reference) |

- State is keyed by the **sequence instance**, so variants of a blueprint, and different blueprints using the same sequence, share progression.
- Each Add step first reserves all explicit values, then fills the rest. Explicit values never advance the counter.
- A `next` function that cycles through reserved values throws instead of hanging.
- Non-numeric sequences work too, for example a rank: `Sequence.Of("a", r => r + "a").PerParent()` (see `tests/Zeeq.Biome.Tests/Board/`).

## Sugar template

```csharp
public static class CodeReviewSugar
{
    extension<TAnchor>(BiomeBuilder<ZeeqSeed, TAnchor> b)            // root: any anchor
    {
        public BiomeBuilder<ZeeqSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository repo) => b.AddOne(Blueprints.Repository, out repo);

        public BiomeBuilder<ZeeqSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository repo, Action<CodeRepository> customize) =>
            b.AddOne(Blueprints.Repository, out repo, customize);

        public BiomeBuilder<ZeeqSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] repos, int count) => b.Add(Blueprints.Repository, out repos, count);

        public BiomeBuilder<ZeeqSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] repos, params Action<CodeRepository>[] customize) =>
            b.Add(Blueprints.Repository, out repos, customize);
    }

    extension(BiomeBuilder<ZeeqSeed, CodeRepository[]> b)            // child: only after a repository
    {
        public BiomeBuilder<ZeeqSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord pr) => b.AddOne(Blueprints.PullRequest, out pr);

        public BiomeBuilder<ZeeqSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord pr, Action<PullRequestRecord> customize) =>
            b.AddOne(Blueprints.PullRequest, out pr, customize);

        public BiomeBuilder<ZeeqSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] prs, int count) => b.Add(Blueprints.PullRequest, out prs, count);

        public BiomeBuilder<ZeeqSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] prs, params Action<PullRequestRecord>[] customize) =>
            b.Add(Blueprints.PullRequest, out prs, customize);
    }
}
```

- Name the methods `Add<Singular>` and `Add<Plural>`, using the domain's own word (`AddPendingInvitation`, not `AddOrganizationMembership`).
- Singular methods return `T` through `out`; plural methods return `T[]`. Both re-anchor to `T[]`.
- The core API underneath:
  - `Add(bp, out T[])`, `Add(bp, out T[], int)`, `Add(bp, out T[], params Action<T>[])`, and `Add(bp, out T[], params Action<T, ctx>[])`.
  - `AddOne(bp, out T[, Action<T>?])` and `AddOne(bp, out T, Action<T, ctx>)`. The ctx form requires a customizer.
  - Input blueprints (`InputBlueprint`, `ChildInputBlueprint`) have the same shapes, with customizers over `TInput` (`Action<TInput>`, or `Action<TInput, BiomeContext<TSeed>>` / `Action<TInput, ChildContext<TSeed, TParent>>`). `out` and the new anchor hold the output.
- A singular child `AddOne` throws unless the anchor has exactly one parent.

## Input blueprints

Use an input when the output has init-only properties (including positional record properties) that tests vary, when one choice drives several values, or when the output is a group of rows. The test customizes a small settable **input**; the factory reads it and builds the row. Production models don't change.

> Ordinary entity outputs and `ChildFromInput` are not in `Zeeq.Biome` 1.0.1; 1.0.1 `FromInput` requires an `IBiomeAggregate` output.

```csharp
public sealed class EnrollmentInput          // construction data: settable, nothing required
{
    public int Seat { get; set; }
    public bool Waitlisted { get; set; }
    public DateTimeOffset EnrolledAtUtc { get; set; }
}

public static readonly Sequence<int> EnrollmentSeats = Sequence.Of(1, n => n + 1).PerParent();

public static readonly ChildInputBlueprint<SchoolSeed, SchoolClass, EnrollmentInput, Enrollment> Enrollment =
    Blueprint
        .ChildFromInput<SchoolSeed, SchoolClass, EnrollmentInput, Enrollment>(
            () => new(),
            (schoolClass, input, ctx) => new()           // Enrollment has required init members
            {
                Id = ctx.NewId("enr"),
                SchoolId = ctx.Ancestor<School>().Id,
                ClassId = schoolClass.Id,
                Seat = input.Seat,
                Status = input.Waitlisted ? "waitlisted" : "active",
                EnrolledAtUtc = input.EnrolledAtUtc,
            })
        .Derive(i => i.Seat, EnrollmentSeats)
        .Derive(i => i.EnrolledAtUtc, (_, ctx) => ctx.Now);
```

- Root: `Blueprint.FromInput<TSeed, TInput, T>(() => new(), (input, ctx) => …)`. Child: `Blueprint.ChildFromInput<TSeed, TParent, TInput, T>(() => new(), (parent, input, ctx) => …)`. `TParent`, `TInput` and `T` are reference types.
- Order in one Add step: every input is created and gets its traits and customizers; `Derive(prop, sequence)` reserves explicit values across the whole step; then, per input in order, its `Derive`s run and the factory builds the output; global defaults then apply to the output, which is recorded.
- Input factories, derives, traits and customizers see only earlier completed steps through `ctx.All<T>()` / `ctx.Last<T>()`. Wire relationships through the typed parent and `ctx.Ancestor<T>()`.
- An input isn't recorded or saved and can't be a parent. A root input blueprint doesn't get a parent from `.Under(…)`; use `ChildFromInput` for that.
- `Derive` on an input fills only an unset property (`default`). On an `int?`/`bool?` input an explicit `0` or `false` is kept, but an explicit `null` is still refilled if a `Derive` targets that property. Global `Default`s can also replace `null`, `0` or `false` on the output. Biome has no presence API.
- `.With`, `.Derive` and `.AsTransient()` work on both input forms.

### Aggregates

```csharp
public sealed record OrganizationGraph(Organization Organization, Team RootTeam, OrganizationMembership Membership)
    : IBiomeAggregate
{
    public IEnumerable<object> GetEntities() => [Organization, RootTeam, Membership];   // FK-safe order
}
```

- Build the blueprint with `Blueprint.FromInput<ZeeqSeed, OrganizationInput, OrganizationGraph>(() => new(), (input, ctx) => …)`. The input is a small settable class, effectively the old prototype.
- `GetEntities()` may return nested aggregates; they are expanded recursively, and duplicate references are saved once.
- `.AsTransient()` on an aggregate excludes all of its parts from the save.
- Parts aren't tracked as parents. A child of an aggregate takes the aggregate type as `TParent`. Global defaults reach the wrapper, not its parts.

## Options

```csharp
private static readonly BiomeOptions<ZeeqSeed> Options = new BiomeOptions<ZeeqSeed>()
    .SeedEntities(seed => [seed.Owner, seed.Organization])
    .Default<IUpdatedEntity, DateTimeOffset>(e => e.UpdatedAtUtc, (_, ctx) => ctx.Now)
    .BeforeSave((save, _) =>
    {
        // save.Seed, save.Session.All<T>(), and the mutable save.Entities list (seed rows first).
        // Add invariant rows here; they skip traits, Derives, and global defaults.
        return ValueTask.CompletedTask;
    })
    .TimePrecision(TimeSpan.FromTicks(10))          // the default: 1 µs
    .Ids(prefix => $"{prefix}_{Guid.CreateVersion7():N}");
```

- `Default` requires explicit type arguments (`Default<TBase, TProp>`). The selector must be a directly writable property, and `init` properties are fine. It throws `ArgumentException` otherwise.
- Hooks run once per `BuildAsync`, in registration order. `AfterSave` runs only if the store's save succeeded.
- When a test needs different options, give `TestBiome` a `NewOptions()` factory and build a fresh instance. Never mutate the shared one.

## Stores

- `NullStore.Instance` is the default and saves nothing.
- `EfCoreStore(dbContext)` (package `Zeeq.Biome.EntityFrameworkCore`) adds all rows in order and calls `SaveChangesAsync` once.
- Custom stores implement `IBiomeStore.SaveAsync(IReadOnlyList<object> entities, CancellationToken)`. The rows arrive with aggregates expanded, transients dropped, duplicates removed, and `BeforeSave` already applied.
