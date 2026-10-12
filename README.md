# Zeeq Biome

[![NuGet](https://img.shields.io/nuget/v/Zeeq.Biome.svg)](https://www.nuget.org/packages/Zeeq.Biome)
[![CI](https://github.com/zeeq-ai/zeeq-biome/actions/workflows/ci.yml/badge.svg)](https://github.com/zeeq-ai/zeeq-biome/actions/workflows/ci.yml)

Terse, readable test setup for .NET. A **biome** is your app's entity graph in miniature: just the rows one test needs, wired together correctly. You declare one **blueprint** per entity type that produces valid, uninteresting defaults. Tests then build scenarios as short fluent chains that mention only the properties under test. Parents and children are typed, numbering and ordering come from sequences, the clock is frozen, and persistence is pluggable (EF Core or none). Defaults never overwrite a value the test set.

```csharp
await TestBiome.Start(_db, out var seed)
    .AddCodeRepository(out var repo)
    .AddPullRequest(out var pr, p => p.State = PullRequestState.Closed)   // the property under test
    .BuildAsync();
```

## Install

```sh
dotnet add package Zeeq.Biome
dotnet add package Zeeq.Biome.EntityFrameworkCore   # optional: save with a DbContext
```

Requires .NET 10 and C# 14. The core package has no dependencies. Works with any test framework; the examples use [TUnit](https://tunit.dev).

## Step by step

The examples model a code-review app: `Organization`, `User`, `CodeRepository` → `PullRequestRecord` → `CodeReviewRecord` → `ActiveCodeReviewLock`. Your entities are plain classes. Entities and parents must be reference types.

### 1. A seed and a `TestBiome` entry point

The **seed** is the baseline every test starts from: an owner, an organization, whatever your app always needs. It can be any type. Wrap `Biome.Start` once in your test project so tests can start with one call.

```csharp
using Zeeq.Biome;

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
        .SeedEntities(seed => [seed.Owner, seed.Organization]);   // saved first, in this order

    public static BiomeBuilder<ZeeqSeed, ZeeqSeed> Start(
        out ZeeqSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null)
    {
        seed = ZeeqSeed.Generate();
        return Biome.Start(seed, Options, store, time);
    }
}
```

- Options are shared by all tests. They freeze on the first `Start`, and changing them afterwards throws.
- Each `Start` creates a new session, so each test gets its own entities, sequences and clock. Parallel tests are safe.

### 2. A blueprint, and your first `Add`

A blueprint is a `static readonly` value with **one factory** per type. The factory gets a context (`ctx`) with the seed, the clock and ID generation.

```csharp
public static partial class Blueprints
{
    public static readonly Blueprint<ZeeqSeed, CodeRepository> Repository =
        Blueprint.For<ZeeqSeed, CodeRepository>(ctx => new()
        {
            Id = ctx.NewId("repo"),                    // "repo_<uuidv7>"
            OrganizationId = ctx.Seed.Organization.Id,
            OwnerQualifiedName = "zeeq-ai/example",
            Enabled = true,
            CreatedAtUtc = ctx.Now,                    // never DateTimeOffset.UtcNow
        });
}
```

```csharp
public sealed class CodeRepositoryTests
{
    [Test]
    public async Task Repository_belongs_to_seed_organization()
    {
        await TestBiome.Start(out var seed)
            .AddOne(Blueprints.Repository, out var repo)
            .BuildAsync();

        await Assert.That(repo.OrganizationId).IsEqualTo(seed.Organization.Id);
    }
}
```

| Shape | Creates | `out` |
|---|---|---|
| `AddOne(bp, out var x)` | 1 default | `T` |
| `AddOne(bp, out var x, x => …)` | 1 customized | `T` |
| `Add(bp, out var xs)` | 1 default | `T[]` |
| `Add(bp, out var xs, 3)` | 3 defaults | `T[]` |
| `Add(bp, out var xs, x => …, x => …)` | one per customizer | `T[]` |

- **Adds are eager.** `repo` exists as soon as `AddOne` returns, and `BuildAsync` saves everything once. Calling it a second time throws.
- **Every Add takes an `out`.** Write `out _` when the test doesn't need the value.

### 3. Customize only what the test is about

The factory supplies the defaults, and the call site sets only the property being tested:

```csharp
await TestBiome.Start(out _)
    .AddOne(Blueprints.Repository, out var disabled, r => r.Enabled = false)
    .Add(Blueprints.Repository, out var pair,
        r => r.OwnerQualifiedName = "zeeq-ai/api",
        r => r.OwnerQualifiedName = "zeeq-ai/web")
    .BuildAsync();
```

**Traits** are named `Action<T>` values, so you can pass them anywhere a customizer goes. **A comma makes a new entity; `+` stacks traits on the same entity.**

```csharp
public static class Repositories
{
    public static readonly Action<CodeRepository> Disabled = r => r.Enabled = false;
    public static readonly Action<CodeRepository> Hidden = r => r.VisibleInLibraryPicker = false;
}
```

```csharp
await TestBiome.Start(out _)
    .Add(Blueprints.Repository, out var repos,
        Repositories.Disabled,                        // repos[0]: disabled
        Repositories.Disabled + Repositories.Hidden)  // repos[1]: disabled and hidden
    .BuildAsync();
```

Variants are new blueprint values. The original is unchanged.

```csharp
public static partial class Blueprints
{
    public static readonly Blueprint<ZeeqSeed, CodeRepository> DisabledRepository =
        Repository.With(Repositories.Disabled);
}
```

Customizers that need the context take two parameters, `(r, ctx) => r.CreatedAtUtc = ctx.Ago(TimeSpan.FromDays(30))`. C# can't mix one- and two-parameter lambdas in a single call, so when you mix them, write all of them in the two-parameter form.

**Derived defaults are fill-only.** `.Derive` assigns a property only if it still has its default value after the traits and customizers have run. A value the test set is never overwritten.

```csharp
public static partial class Blueprints
{
    public static readonly Blueprint<ZeeqSeed, OrganizationMembership> PendingInvitation =
        Blueprint.For<ZeeqSeed, OrganizationMembership>(ctx => new()
            {
                Id = ctx.NewId("mem"),
                OrganizationId = ctx.Seed.Organization.Id,
                Role = "member",
                Status = MembershipStatus.Pending,
                InvitedEmail = "invited@example.test",
                CreatedByUserId = ctx.Seed.Owner.Id,
                CreatedAtUtc = ctx.Now,
            })
            .Derive(m => m.ExpiresAtUtc, (m, _) => m.CreatedAtUtc.AddDays(7));
}
```

```csharp
var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
await TestBiome.Start(out _)
    .Add(Blueprints.PendingInvitation, out var invitations,
        i => i.CreatedAtUtc = created,                // ExpiresAtUtc derived → Jan 8
        i => i.ExpiresAtUtc = created.AddDays(-1))    // explicit → kept
    .BuildAsync();
```

"Unset" means `default(TProp)`: `null`, `0`, `false`, `default(DateTimeOffset)`. An empty string `""` counts as set. In the factory, leave derived properties unset.

> **Note:** customizers assign properties, so any property a test varies needs a setter. With `{ get; init; }`, the compiler rejects assignment from a customizer (CS8852). `Derive` can still fill `init` properties.

### 4. Children: chain, branch, fan out

A **child blueprint** takes its parent as a typed argument. `ctx.Ancestor<T>()` reaches further up the chain.

```csharp
public static partial class Blueprints
{
    public static readonly ChildBlueprint<ZeeqSeed, PullRequestRecord, CodeReviewRecord> Review =
        Blueprint.ChildOf<ZeeqSeed, PullRequestRecord, CodeReviewRecord>((pr, ctx) => new()
        {
            Id = ctx.NewId("review"),
            OrganizationId = pr.OrganizationId,
            PullRequestRecordId = pr.Id,
            RepositoryId = ctx.Ancestor<CodeRepository>().Id,   // grandparent
            Status = CodeReviewStatus.Pending,
            CreatedAtUtc = ctx.Now,
        });

    public static readonly ChildBlueprint<ZeeqSeed, CodeReviewRecord, ActiveCodeReviewLock> ActiveLock =
        Blueprint.ChildOf<ZeeqSeed, CodeReviewRecord, ActiveCodeReviewLock>((review, ctx) => new()
        {
            OrganizationId = review.OrganizationId,
            RepositoryId = review.RepositoryId,
            PullRequestRecordId = review.PullRequestRecordId,
            CodeReviewRecordId = review.Id,
            AcquiredAtUtc = ctx.Now,
            ExpiresAtUtc = ctx.FromNow(TimeSpan.FromMinutes(5)),
        });
}
```

(`Blueprints.PullRequest`, a child of `CodeRepository`, is shown in [step 6](#6-sequences-for-numbers-and-ordering).)

**Every Add re-anchors to its own output**, so a chain reads top to bottom:

```csharp
await TestBiome.Start(out _)
    .AddOne(Blueprints.Repository, out var repo)
    .AddOne(Blueprints.PullRequest, out var pr)        // parent: repo
    .AddOne(Blueprints.Review, out var review)         // parent: pr
    .AddOne(Blueprints.ActiveLock, out var activeLock) // parent: review
    .BuildAsync();
```

- A child Add **compiles only when the current anchor is its parent type**. For example, adding a `Review` directly after a repository is a compile error.
- `.Under(x)` jumps back to an earlier Add output, or to a seed row listed in `SeedEntities`, so you can add siblings or start another branch. Objects the session didn't create are rejected.
- **Fan-out is per parent.** Each customizer runs once for each parent, and the results come back as one flat array ordered by parent:

```csharp
await TestBiome.Start(out _)
    .Add(Blueprints.Repository, out var repos, 2)
    .Add(Blueprints.PullRequest, out var prs, 3)   // 3 per repo → 6: prs[0..2] → repos[0], prs[3..5] → repos[1]
    .BuildAsync();
```

A singular child `AddOne` throws unless the anchor holds exactly one parent.

### 5. Sugar: four one-liners per type, plus scenario helpers

Give each type four short extension methods: singular and plural, each with and without customizers. Child sugar is declared on the parent's anchor type, so IntelliSense offers `AddPullRequest` only right after a repository.

```csharp
public static class CodeReviewSugar
{
    // Root type: offered from any anchor.
    extension<TAnchor>(BiomeBuilder<ZeeqSeed, TAnchor> b)
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

    // Child type: offered only when the anchor is CodeRepository[].
    extension(BiomeBuilder<ZeeqSeed, CodeRepository[]> b)
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

    // …the same four for CodeReviewRecord (AddCodeReview/s) and ActiveCodeReviewLock (AddActiveLock/s).

    // Scenario helper: a whole default chain, offered from the seed only.
    extension(BiomeBuilder<ZeeqSeed, ZeeqSeed> b)
    {
        public BiomeBuilder<ZeeqSeed, ActiveCodeReviewLock[]> AddLockedReview(
            out ActiveCodeReviewLock activeLock) =>
            b.AddCodeRepository(out _)
                .AddPullRequest(out _)
                .AddCodeReview(out _)
                .AddActiveLock(out activeLock);
    }
}
```

A lock test now names only the lock:

```csharp
await TestBiome.Start(_db, out _).AddLockedReview(out var activeLock).BuildAsync();
```

In zeeq-app, the same setup took a five-step `EntityGraph` chain, a five-slot tuple discard and `activeLocks[0]`. Each scenario helper's whole chain is visible in its definition. Biome has no hidden auto-creation of ancestors.

### 6. Sequences for numbers and ordering

A **sequence** is a value you declare once: a start value and a `next` function. Its state lives in each session, so it continues across Add calls and never leaks between tests. Assign it with fill-only `Derive`:

```csharp
public static partial class Blueprints
{
    public static readonly Sequence<int> PullRequestNumbers =
        Sequence.Of(1, n => n + 1).Per<CodeRepository>();   // numbering restarts per repository

    public static readonly ChildBlueprint<ZeeqSeed, CodeRepository, PullRequestRecord> PullRequest =
        Blueprint.ChildOf<ZeeqSeed, CodeRepository, PullRequestRecord>((repo, ctx) => new()
            {
                Id = ctx.NewId("pr"),
                OrganizationId = repo.OrganizationId,
                RepositoryId = repo.Id,
                State = PullRequestState.Open,
                CreatedAtUtc = ctx.Now,
            })
            .Derive(pr => pr.PullRequestNumber, PullRequestNumbers)
            .Derive(pr => pr.Title, (pr, _) => $"Pull request {pr.PullRequestNumber}")   // runs after the number
            .Derive(pr => pr.Branch, (pr, _) => $"feature/{pr.PullRequestNumber}")
            .Derive(pr => pr.GitHubNodeId, (_, ctx) => ctx.NewId("PR"));                // globally unique
}
```

| Scope | One counter per | Example |
|---|---|---|
| `.PerSession()` (default) | test | anything unique across the whole test |
| `.PerParent()` | immediate parent | card rank within a column |
| `.Per<TAncestor>()` | nearest ancestor of that type | PR number within a repository |

```csharp
await TestBiome.Start(out _)
    .AddCodeRepositories(out var repos, 2)
    .AddPullRequests(out var first, 2)       // 1, 2 | 1, 2
    .Under(repos[0])
    .AddPullRequest(out var third)           // 3: resumes repos[0]
    .Under(repos[0])
    .AddPullRequests(out var mixed,
        _ => { },                            // 5 (4 is taken below)
        pr => pr.PullRequestNumber = 4)      // explicit: kept, reserved
    .BuildAsync();
```

- **Explicit values win.** They don't move the sequence, and the sequence skips any value already set explicitly in that scope, whether in this step or an earlier one. It can't know about values set in a *later* step, so set explicit values in or before the step that fills.
- `Derive`s run in declaration order. Derive values like `Title` after the number they depend on.
- Variants made with `.With` share their base blueprint's sequence.
- For globally unique values, use a `PerSession` sequence or `ctx.NewId`.
- In a static class, fields initialize top to bottom. Declare a sequence above the blueprints that use it, and keep a base blueprint and its variants in the same file. Across `partial` files, the initialization order is unspecified.

### 7. Time

- The session has **one clock**. In blueprints, `ctx.Now`, `ctx.Ago(…)` and `ctx.FromNow(…)` are the only time sources.
- If you pass no clock, time is **frozen** when `Start` is called. Every entity in the test sees the same `Now`.
- If you pass a `TimeProvider`, it is read **live**. Share the same `FakeTimeProvider` with the system under test and advance it during the test:

```csharp
var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
await TestBiome.Start(out _, time: time)
    .AddLockedReview(out var activeLock)      // ExpiresAtUtc = 12:05
    .BuildAsync();

time.Advance(TimeSpan.FromMinutes(6));        // the SUT reads the same clock: the lock has expired
```

- **Values are rounded to 1 µs by default**, whichever clock you use. That matches Postgres `timestamptz`, so values you read back compare equal to the ones you wrote. Change it with `options.TimePrecision(TimeSpan.FromMilliseconds(1))`, or turn it off with `TimeSpan.Zero`.

### 8. Save: EF Core, or no database

`BuildAsync` passes the rows to an `IBiomeStore`. The seed's rows come first, then created entities in creation order.

```csharp
using Microsoft.EntityFrameworkCore;
using Zeeq.Biome.EntityFrameworkCore;

public static partial class TestBiome
{
    public static BiomeBuilder<ZeeqSeed, ZeeqSeed> Start(
        DbContext db, out ZeeqSeed seed, TimeProvider? time = null) =>
        Start(out seed, new EfCoreStore(db), time);
}
```

```csharp
await TestBiome.Start(_db, out var seed)
    .AddLockedReview(out var activeLock)
    .BuildAsync();                 // AddRange in order, one SaveChangesAsync

_db.ChangeTracker.Clear();         // so the code under test reads rows from the database, not tracked instances
```

- `db.AsBiomeStore()` is shorthand for `new EfCoreStore(db)`.
- **No database:** omit the store. `NullStore` saves nothing, and entities still exist as soon as each Add returns.
- **Database-generated keys:** set navigation properties in child factories, for example `Repository = repo`, and EF fills in the foreign keys.
- **Anything else:** implement `IBiomeStore`, a single `SaveAsync(IReadOnlyList<object>, CancellationToken)` method.

## Use cases and scenarios

These examples use the sugar from step 5, the same four methods for `User` (`AddUser` / `AddUsers`), and these traits:

```csharp
public static class PullRequests
{
    public static readonly Action<PullRequestRecord> Closed = pr => pr.State = PullRequestState.Closed;
    public static readonly Action<PullRequestRecord> Merged = pr => pr.State = PullRequestState.Merged;
    public static readonly Action<PullRequestRecord> Draft = pr => pr.IsDraft = true;
}
```

### Linear chains: name what you assert, discard the rest

```csharp
await TestBiome.Start(_db, out _)
    .AddCodeRepository(out _)
    .AddPullRequest(out _, PullRequests.Merged)
    .AddCodeReview(out var review, r => r.Status = CodeReviewStatus.Running)
    .BuildAsync();
```

### Siblings and branches with `.Under`

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .AddPullRequest(out var open)
    .Under(repo)                                   // back to the repository
    .AddPullRequest(out var closed, PullRequests.Closed)
    .AddCodeReview(out var review)                 // reviews `closed`
    .BuildAsync();
```

### Fan-out per parent, with parent-aware customizers

Child customizers in the two-parameter form get `ctx.Parent`, `ctx.ParentIndex`, and `ctx.Index` (the position under that parent):

```csharp
public static partial class Blueprints
{
    public static readonly ChildBlueprint<ZeeqSeed, User, UserAlias> UserAlias =
        Blueprint.ChildOf<ZeeqSeed, User, UserAlias>((user, ctx) => new()
            {
                Id = ctx.NewId("alias"),
                OrganizationId = ctx.Seed.Organization.Id,
                UserId = user.Id,
                Kind = UserAliasKind.GitHub,
                DisplayValue = $"{user.Id}@example.test",
            })
            .Derive(a => a.NormalizedValue, (a, _) => a.DisplayValue.Trim().TrimStart('@').ToLowerInvariant());
}
```

```csharp
await TestBiome.Start(out _)
    .AddUsers(out var users, u => u.DisplayName = "Ada", u => u.DisplayName = "Grace")
    .Add(Blueprints.UserAlias, out var aliases,
        (alias, ctx) => alias.DisplayValue = $"@{ctx.Parent.DisplayName}")
    .BuildAsync();
// aliases: "@Ada" → "ada", "@Grace" → "grace"; two customizers would give 4 aliases (2 per user)
```

### Transient "input" entities

Sometimes the code under test should create a row, and the test only needs to build the input. `.Transient()` excludes the **most recent Add step** from the save, but the entities are still returned:

```csharp
await TestBiome.Start(_db, out _)
    .AddOne(Blueprints.PendingInvitation, out var invitation, i => i.InvitedEmail = "new@example.test")
    .Transient()                                 // put it right after the step it marks
    .BuildAsync();
// act: await invitations.CreateAsync(invitation);
```

To make a blueprint transient everywhere it's used, declare it with `Blueprints.PendingInvitation.AsTransient()`.

With EF Core, a transient entity that a saved row references through a navigation property is still saved: EF follows the reference. Keep transient inputs unreferenced.

### Ordering with sequences

```csharp
public static partial class Blueprints
{
    public static readonly Sequence<int> FindingPositions = Sequence.Of(1, n => n + 1).PerParent();

    public static readonly ChildBlueprint<ZeeqSeed, CodeReviewRecord, ReviewFinding> Finding =
        Blueprint.ChildOf<ZeeqSeed, CodeReviewRecord, ReviewFinding>((review, ctx) => new()
            {
                Id = ctx.NewId("finding"),
                CodeReviewRecordId = review.Id,
            })
            .Derive(f => f.Position, FindingPositions);
}
```

Positions continue per review across Add calls. `.Under` takes an entity you captured with `out`:

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out _)
    .AddPullRequest(out _)
    .AddCodeReview(out var review)
    .Add(Blueprints.Finding, out var findings, 3)    // positions 1, 2, 3
    .Under(review)
    .Add(Blueprints.Finding, out var later)          // position 4: resumes this review
    .BuildAsync();
```

### Time-sensitive tests sharing a clock with the SUT

```csharp
var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
await TestBiome.Start(_db, out _, time)
    .AddCodeRepository(out _)
    .AddPullRequest(out _)
    .AddOne(Blueprints.Review, out var review,
        (r, ctx) => r.CreatedAtUtc = ctx.Ago(TimeSpan.FromMinutes(4)))
    .BuildAsync();
// act: var sweeper = new StaleReviewSweeper(_db, time); time.Advance(…); await sweeper.RunAsync();
```

The four sugar methods take `Action<T>`. For a context-aware customizer, call `AddOne` / `Add` with the blueprint directly, as above.

### Unit tests with no database

```csharp
TestBiome.Start(out _)                                // NullStore; no BuildAsync needed
    .AddCodeRepository(out var repo)
    .AddPullRequests(out var prs, PullRequests.Draft, PullRequests.Merged);

// act: var summary = PullRequestSummary.From(repo, prs);
```

Call `BuildAsync` only when you need a save or save hooks.

### Custom seed shapes

The seed can be anything: a record, a string, an existing fixture object.

```csharp
public sealed record TenantSeed(string Actor, int Tenant);

var options = new BiomeOptions<TenantSeed>();   // usually a static readonly field
var biome = Biome.Start(new TenantSeed("actor", 42), options);
```

### Global defaults and save hooks

**Global defaults** fill unset properties on any entity that matches a base class or interface. They run after the blueprint's own `Derive`s, and like them they never overwrite. **Save hooks** see the final row list and may add or remove rows.

```csharp
private static readonly BiomeOptions<ZeeqSeed> Options = new BiomeOptions<ZeeqSeed>()
    .SeedEntities(seed => [seed.Owner, seed.Organization])
    .Default<IUpdatedEntity, DateTimeOffset>(e => e.UpdatedAtUtc, (_, ctx) => ctx.Now)
    .BeforeSave((save, _) =>
    {
        // Invariant: every saved user is an active member of the seed organization.
        foreach (var user in save.Entities.OfType<User>().ToList())
        {
            save.Entities.Add(new OrganizationMembership
            {
                Id = $"mem_{Guid.CreateVersion7():N}",
                OrganizationId = save.Seed.Organization.Id,
                UserId = user.Id,
                Role = user == save.Seed.Owner ? "owner" : "member",
                Status = MembershipStatus.Active,
                CreatedByUserId = save.Seed.Owner.Id,
            });
        }
        return ValueTask.CompletedTask;
    })
    .AfterSave((save, _) => ValueTask.CompletedTask);   // e.g. logging, assertions on what was saved
```

Use hooks sparingly. Rows a test can't see make it harder to read. Rows added in `BeforeSave` skip the per-entity pipeline: no traits, `Derive`s, or global defaults are applied to them.

### Aggregates: one input, several rows

When one concept is several rows, as with zeeq-app's `OrganizationGraph`, customize a small **input** object and build the rows from it. The aggregate implements `IBiomeAggregate`, and its parts are saved in place of the aggregate itself.

```csharp
public sealed class OrganizationInput
{
    public string DisplayName { get; set; } = "Test Organization";
    public string? Slug { get; set; }
    public string Role { get; set; } = "owner";
    public bool IsDefaultMembership { get; set; }
}

public sealed record OrganizationGraph(Organization Organization, Team RootTeam, OrganizationMembership Membership)
    : IBiomeAggregate
{
    public IEnumerable<object> GetEntities() => [Organization, RootTeam, Membership];   // save order
}

public static partial class Blueprints
{
    public static readonly InputBlueprint<ZeeqSeed, OrganizationInput, OrganizationGraph> Organization =
        Blueprint.FromInput<ZeeqSeed, OrganizationInput, OrganizationGraph>(
                () => new(),
                (input, ctx) =>
                {
                    var owner = ctx.Seed.Owner;
                    var org = new Organization
                    {
                        Id = ctx.NewId("org"),
                        DisplayName = input.DisplayName,
                        Slug = input.Slug,
                        CreatedByUserId = owner.Id,
                    };
                    var team = new Team { Id = ctx.NewId("team"), OrganizationId = org.Id, IsRootTeam = true };
                    var membership = new OrganizationMembership
                    {
                        Id = ctx.NewId("mem"),
                        OrganizationId = org.Id,
                        UserId = owner.Id,
                        Role = input.Role,
                        Status = MembershipStatus.Active,
                        IsDefault = input.IsDefaultMembership,
                        CreatedByUserId = owner.Id,
                    };
                    return new(org, team, membership);
                })
            .Derive(i => i.Slug, (_, ctx) => ctx.NewId("org"));
}
```

```csharp
await TestBiome.Start(_db, out _)
    .Add(Blueprints.Organization, out var orgs,
        o => o.IsDefaultMembership = true,
        o => o.Slug = "beta")
    .BuildAsync();   // saves org, root team, membership for each, in GetEntities() order
```

Use input blueprints for aggregates only. A single entity should be customized directly. Global defaults apply to the aggregate object itself, not to its parts. Parts aren't tracked as parents, so to hang children off an aggregate, make the aggregate type (`OrganizationGraph`) the child blueprint's parent.

## Cheat sheet

| You want | Write |
|---|---|
| Start a test | `TestBiome.Start(out var seed)` / `TestBiome.Start(_db, out var seed, time)` |
| Root blueprint | `Blueprint.For<TSeed, T>(ctx => new() { … })` |
| Child blueprint | `Blueprint.ChildOf<TSeed, TParent, T>((parent, ctx) => new() { … })` |
| Aggregate blueprint | `Blueprint.FromInput<TSeed, TInput, T>(() => new(), (input, ctx) => …)` |
| Variant | `bp.With(trait)` / `bp.With((e, ctx) => …)` |
| Derived default (fill-only) | `bp.Derive(e => e.Prop, (e, ctx) => …)` |
| Sequence | `Sequence.Of(1, n => n + 1)` + `.PerSession()` / `.PerParent()` / `.Per<T>()`; `bp.Derive(e => e.Prop, seq)` |
| Never save | `bp.AsTransient()` (blueprint) / `.Transient()` (most recent step) |
| One / many | `AddOne(bp, out var x[, c])` / `Add(bp, out var xs[, n \| c1, c2…])` |
| Jump back | `.Under(entity)` / `.Under(array)` |
| Context | `ctx.Seed`, `ctx.Now`, `ctx.Ago(t)`, `ctx.FromNow(t)`, `ctx.NewId("p")`, `ctx.Index`, `ctx.Next(seq)`, `ctx.Ancestor<T>()`, `ctx.All<T>()`, `ctx.Last<T>()` |
| Child context | `+ ctx.Parent`, `ctx.ParentIndex` |
| Inspect | `biome.All<T>()`, `biome.Last<T>()` (keep `var biome = TestBiome.Start(…)`) |
| Options | `SeedEntities`, `Default<TBase, TProp>`, `BeforeSave`, `AfterSave`, `TimePrecision`, `Ids` |
| Save | `await biome.BuildAsync(ct)`: once; stores: `NullStore`, `EfCoreStore`, your `IBiomeStore` |

## The one rule

**The call site wins, then the more specific default. Nothing overwrites.**

For each entity in an Add step:

1. **Factory** creates it. IDs are set here with `ctx.NewId`.
2. **Traits** (`.With`) run, in declaration order.
3. **Call-site customizers** run.
4. **`Derive`s** run, fill-only and in declaration order. Sequences first reserve every explicit value in the step, then fill the rest.
5. **Global defaults** (`options.Default`) run, fill-only.
6. The entity is recorded, along with its parent link and step.

`BuildAsync` then:

1. Takes the seed rows, followed by created entities in creation order.
2. Expands aggregates.
3. Drops transients and removes duplicate references.
4. Runs `BeforeSave`.
5. Calls `IBiomeStore.SaveAsync`.
6. Runs `AfterSave`, if the save succeeded.

## Versioning and contributing

- [Semantic Versioning](https://semver.org), computed from [Conventional Commits](https://www.conventionalcommits.org): `feat:` → minor, `fix:` → patch, `feat!:` / `BREAKING CHANGE:` → major.
- Releases are cut by the [Release workflow](https://github.com/zeeq-ai/zeeq-biome/actions/workflows/release.yml) (GitHub Actions → stable or rc). It publishes both packages to nuget.org and tags `vX.Y.Z`.
- Build and test: `dotnet build zeeq-biome.slnx` then `dotnet test --solution zeeq-biome.slnx`. Format: `dotnet csharpier format .`
- Agent skills for writing tests and blueprints: [`.agents/skills`](https://github.com/zeeq-ai/zeeq-biome/tree/main/.agents/skills).
- License: [MIT](https://github.com/zeeq-ai/zeeq-biome/blob/main/LICENSE).
