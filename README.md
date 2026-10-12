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

Targets .NET 10. The core package has no dependencies. The extension-block examples use C# 14; generated graft methods use regular extension methods. Works with any test framework; the examples use [TUnit](https://tunit.dev).

Ordinary-entity input blueprints and typed child inputs are available in `Zeeq.Biome` 1.1.0 and later. Use the matching version of `Zeeq.Biome.EntityFrameworkCore` when needed.

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
[BiomeGraft]
public static class Blueprints
{
    [Graft("CodeRepository", Plural = "CodeRepositories")]
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
            .AddCodeRepository(out var repo)
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

`[BiomeGraft]` on the catalog generates the `AddCodeRepository` methods from its blueprint fields. Roots are available from any anchor; a child graft is available only from its declared parent array. `[Graft("Name", Plural = "Names")]` overrides the method suffixes, and `[NoGraft]` excludes a catalog member. See [Generated grafts and scenario helpers](https://zeeq-ai.github.io/zeeq-biome/guides/scenario-helpers-and-sugar).

### 3. Customize only what the test is about

The factory supplies the defaults, and the call site sets only the property being tested:

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var disabled, r => r.Enabled = false)
    .AddCodeRepositories(out var pair,
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

> **Note:** customizers assign properties, so any property a test varies on the entity needs a setter. With `{ get; init; }`, the compiler rejects assignment from a customizer (CS8852). `Derive` can still fill `init` properties. For rows that must stay immutable, customize an input instead; see [Input blueprints](#input-blueprints-customize-an-input-then-build-the-row).

### 4. Children: chain, branch, fan out

A **child blueprint** takes its parent as a typed argument. `ctx.Ancestor<T>()` reaches further up the chain.

```csharp
public static partial class Blueprints
{
    public static readonly ChildBlueprint<ZeeqSeed, CodeRepository, PullRequestRecord> PullRequest =
        Blueprint.ChildOf<ZeeqSeed, CodeRepository, PullRequestRecord>((repo, ctx) => new()
        {
            Id = ctx.NewId("pr"),
            RepositoryId = repo.Id,
            CreatedAtUtc = ctx.Now,
        });

    [Graft("CodeReview", Plural = "CodeReviews")]
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

    [Graft("ActiveLock", Plural = "ActiveLocks")]
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
    .AddCodeRepository(out var repo)
    .AddPullRequest(out var pr)        // parent: repo
    .AddCodeReview(out var review)     // parent: pr
    .AddActiveLock(out var activeLock) // parent: review
    .BuildAsync();
```

- A child graft **compiles only when the current anchor is its parent type**. For example, adding a review directly after a repository is a compile error.
- `.Under(x)` jumps back to an earlier Add output, or to a seed row listed in `SeedEntities`, so you can add siblings or start another branch. Objects the session didn't create are rejected.
- **Fan-out is per parent.** Each customizer runs once for each parent, and the results come back as one flat array ordered by parent:

```csharp
await TestBiome.Start(out _)
    .AddCodeRepositories(out var repos, 2)
    .AddPullRequests(out var prs, 3)   // 3 per repo → 6: prs[0..2] → repos[0], prs[3..5] → repos[1]
    .BuildAsync();
```

A singular child graft throws unless the anchor holds exactly one parent.

### 5. Generated grafts and scenario helpers

The generator emits three graft methods for each eligible member with valid names in a `[BiomeGraft]` catalog: one singular method with an optional `Action<T>` customizer, and two plural methods (count or `params` customizers). For example, a `Repository` field renamed with `[Graft("CodeRepository", Plural = "CodeRepositories")]` produces `AddCodeRepository` and `AddCodeRepositories` in `BlueprintsGrafts`.

```csharp
[BiomeGraft]
public static class Blueprints
{
    [Graft("CodeRepository", Plural = "CodeRepositories")]
    public static readonly Blueprint<ZeeqSeed, CodeRepository> Repository = null!;

    public static readonly ChildBlueprint<ZeeqSeed, CodeRepository, PullRequestRecord> PullRequest =
        null!;

    [Graft("CodeReview", Plural = "CodeReviews")]
    public static readonly ChildBlueprint<ZeeqSeed, PullRequestRecord, CodeReviewRecord> Review =
        null!;

    [Graft("ActiveLock", Plural = "ActiveLocks")]
    public static readonly ChildBlueprint<ZeeqSeed, CodeReviewRecord, ActiveCodeReviewLock> ActiveLock =
        null!;
}
```

Root graft methods are generic in the anchor and work from any step with the same seed; their count or customizer list applies once at the current anchor. Child graft methods accept only the parent-array anchor, fan out for each parent, and do not create ancestors. Child counts and customizers apply to each parent; a plural call with no customizers creates one default output per parent (or one for a root). A root and child with colliding method signatures on the same builder produce `BIOME001`.

Use the core `Add` / `AddOne` API with a blueprint for a one-off context-aware customizer. Handwrite an `AddX` extension only for a repeated context-aware overload or another app-specific exception. Keep named multi-step chains as scenario helpers:

```csharp
public static class CodeReviewScenarioHelpers
{
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

These examples use the generated grafts from step 5, including `AddUser` / `AddUsers`, and these traits:

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

Generated graft customizers take `Action<T>` (or `Action<TInput>` for an input blueprint). For a one-off context-aware customizer, call core `AddOne` / `Add` with the blueprint directly. If tests repeatedly need the clock or parent for a type, add only a named handwritten context overload for that type.

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

### Input blueprints: customize an input, then build the row

> **Package API:** ordinary entity outputs from `FromInput` and `ChildFromInput` require a referenced package that exposes those forms. Check the package API before using them, and align the EF Core package version. This README does not identify a package release for these APIs.

A customizer runs after the factory, so it cannot set an init-only property. (A `required` member with a setter can still be customized.) An **input blueprint** lets tests customize a small settable input, then the factory reads it to build the row, so production models stay as they are. If `PullRequestRecord.PullRequestNumber` were `init`, as in zeeq-app's model, it would look like this:

```csharp
public sealed class PullRequestInput
{
    public int Number { get; set; }
    public string? Title { get; set; }
}

public static readonly ChildInputBlueprint<ZeeqSeed, CodeRepository, PullRequestInput, PullRequestRecord> PullRequestFromInput =
    Blueprint
        .ChildFromInput<ZeeqSeed, CodeRepository, PullRequestInput, PullRequestRecord>(
            () => new(),
            (repo, input, ctx) => new()
            {
                Id = ctx.NewId("pr"),
                OrganizationId = repo.OrganizationId,
                RepositoryId = repo.Id,
                PullRequestNumber = input.Number,         // init-only: set during construction
                Title = input.Title!,
                State = PullRequestState.Open,
                CreatedAtUtc = ctx.Now,
            })
        .Derive(i => i.Number, PullRequestNumbers)        // fills the input; explicit numbers are reserved
        .Derive(i => i.Title, (i, _) => $"Pull request {i.Number}");
```

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .Add(Blueprints.PullRequestFromInput, out var prs, 2)                          // numbers 1, 2
    .Under(repo)
    .AddOne(Blueprints.PullRequestFromInput, out var pr10, i => i.Number = 10)    // explicit number
    .BuildAsync();
```

- `Blueprint.FromInput` builds a root. `Blueprint.ChildFromInput` builds a child and passes the typed parent to the factory; `ctx.Ancestor<T>()` and per-parent fan-out work as for `ChildOf`.
- Traits, customizers and `Derive`s act on the **input**. `out` variables and the new anchor hold the **output**.
- An input is construction data: it is not recorded or saved, can't be a parent, and `All<PullRequestInput>()` is empty. A root input blueprint stays a root after `.Under(…)`.
- `Derive` treats a property's default value (`null`, `0`, `false`) as unset. A nullable value type (`int?`) keeps an explicit `0`/`false` but not an explicit `null`. Global defaults can also replace `null`, `0` or `false` on the output.

See the [Input blueprints guide](https://zeeq-ai.github.io/zeeq-biome/guides/input-blueprints) for the order of work in a step and the aggregate form.

#### Aggregates: one input, several rows

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

Global defaults apply to the aggregate object itself, not to its parts. Parts aren't tracked as parents, so to hang children off an aggregate, make the aggregate type (`OrganizationGraph`) the child blueprint's parent.

## Cheat sheet

| You want | Write |
|---|---|
| Start a test | `TestBiome.Start(out var seed)` / `TestBiome.Start(_db, out var seed, time)` |
| Call without naming a blueprint | `[BiomeGraft]` catalog generates `{Catalog}Grafts`; use `[Graft]` for names and `[NoGraft]` to skip a member |
| Root blueprint | `Blueprint.For<TSeed, T>(ctx => new() { … })` |
| Child blueprint | `Blueprint.ChildOf<TSeed, TParent, T>((parent, ctx) => new() { … })` |
| Root built from an input | `Blueprint.FromInput<TSeed, TInput, T>(() => new(), (input, ctx) => …)` |
| Child built from an input | `Blueprint.ChildFromInput<TSeed, TParent, TInput, T>(() => new(), (parent, input, ctx) => …)` |
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

For an input blueprint, every input first gets its traits and customizers (steps 2 and 3); then, per input, its `Derive`s (step 4) and the factory run in order; steps 5 and 6 apply to each output.

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
