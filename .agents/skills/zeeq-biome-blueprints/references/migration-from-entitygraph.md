# Migrating from zeeq-app `EntityGraph`

The source is `zeeq-app/src/backend/Zeeq.Testing/EntityGraph/`: `EntityGraphBuilder.cs`, `SeedContext.cs`, and `EntityGraph*Extensions.cs`. The "after" code below targets zeeq-app's real `Zeeq.Core.Models` types. "Before" snippets are the old API and are marked `text`.

## Concept map

| EntityGraph | Biome |
|---|---|
| `EntityGraph.AddGeneratedSeed(_context)` / `SeedContext.Generate()` | `TestBiome.Start(_context, out var seed)`. `SeedContext` itself can be the seed type. |
| `*Prototype` class + `Create*` method + 2–3 overloads | one blueprint + `Derive`s + the four sugar one-liners. If the entity keeps `init` properties tests vary, the prototype becomes the **input** of an input blueprint (step 3). |
| `prototype.X ?? default` | factory default, or fill-only `.Derive(e => e.X, …)` |
| `PersistOnBuild = false` | `.Transient()` after the step, or `bp.AsTransient()` |
| `FindLatest<CodeRepository>(builder.Entities)` | a child blueprint's typed parent, plus `ctx.Ancestor<CodeRepository>()` |
| `PullRequestNumber = index + 1` (restarts every call) | `Sequence.Of(1, n => n + 1).Per<CodeRepository>()` + `.Derive(pr => pr.PullRequestNumber, seq)` |
| `var (_, _, _, _, locks) = await … .BuildAsync(); var l = locks[0];` | `.AddActiveLock(out var l)`; `BuildAsync()` returns nothing |
| `OrganizationGraph` + `OrganizationPrototype` | `InputBlueprint` (the prototype becomes the input) and `IBiomeAggregate` |
| `*Prototype` customized before an immutable row is built (alias, invitation, numbered PR) | `Blueprint.FromInput` / `ChildFromInput`: tests customize the input, the factory builds the `init` row |
| `DateTimeOffset.UtcNow` + `TruncateToPostgresPrecision()` | `ctx.Now` (rounded to 1 µs by default) / a shared `FakeTimeProvider` |
| hard-coded cases in `PersistStateAsync` (seed dedupe, `OrganizationGraph`, enumerables) | `SeedEntities`, `IBiomeAggregate`, reference dedupe, and `EfCoreStore` |

## Step 1: entry point (reuse `SeedContext`)

```csharp
public static class TestBiome
{
    private static readonly BiomeOptions<SeedContext> Options = new BiomeOptions<SeedContext>()
        .SeedEntities(seed => seed.GetSeedEntities());

    public static BiomeBuilder<SeedContext, SeedContext> Start(
        DbContext db, out SeedContext seed, TimeProvider? time = null)
    {
        seed = SeedContext.Generate();
        return Biome.Start(seed, Options, new EfCoreStore(db), time);
    }
}
```

`SeedContext.Generate()` stamps rows with `DateTimeOffset.UtcNow`, which isn't rounded. If a test compares seed timestamps after a round trip, move the seed onto the session clock.

**An app-wide clock.** If the code under test reads an app-shared `TimeProvider`, pass that same instance as `time` to `TestBiome.Start`. Biome rounds the readings it takes for `ctx.Now` / `Ago` / `FromNow` (1 µs by default), but the code under test still reads the unrounded provider. When the code under test writes timestamps that tests compare after a database round trip, give both sides one rounded clock. Do that in your test project, for example with a small `TimeProvider` subclass whose `GetUtcNow()` truncates to the database precision. Biome has no public API for this, so don't look for one.

## Step 2: first, check whether properties are `init`

zeeq-app entities make many properties `init`. Examples:
- `DomainEntityBase.CreatedAtUtc`
- `PullRequestRecord.PullRequestNumber`
- `UserAlias.Kind` and `UserAlias.NormalizedValue`
- `OrganizationMembership.InvitedEmail`, `ExpiresAtUtc` and `UserId`
- `ActiveCodeReviewLock.AcquiredAtUtc`

**A customizer can't assign an `init` property (CS8852)**, and prototypes hid this: they customized a settable object *before* the factory built the immutable row. Keep that shape. **Preserve the production `init` models** and don't change setters just so tests can customize them. Pick one approach per property:

| Situation | Approach |
|---|---|
| Tests never vary it | Set it in the factory. `Derive` can also fill `init` properties, since it uses a compiled setter. |
| It's a `required` init property derived from others | Assign `null!` / `default` in the factory, then `.Derive(...)`. |
| It's a discriminator (an enum like `UserAliasKind`) | One blueprint per value with a private factory method (step 4), or a `Kind` property on an input. |
| Tests vary it, and it is `init` | Use an **input blueprint** (`FromInput` / `ChildFromInput`): the prototype's settable properties become the input, and the factory sets the `init` values (step 3). |
| Tests vary it, and the model already has a `set` | Customize the entity directly. |

An input blueprint needs a Zeeq.Biome release newer than 1.0.1; ordinary entity outputs and `ChildFromInput` are not in 1.0.1. Until one is published, keep the old builder for those types, and don't pin 1.0.1 expecting these APIs.

## Step 3: a prototype with derived defaults (`PendingInvitationPrototype`)

Before (shape):

```text
var createdAt = prototype.CreatedAtUtc ?? now;
… InvitedEmail = prototype.Email ?? $"{SeedContext.NewId("invite")}@example.test",
  ExpiresAtUtc = prototype.ExpiresAtUtc ?? createdAt.AddDays(7),
… if (!prototype.PersistOnBuild) nonPersistentInvitations.Add(invitation);
```

After, with the model's `init` properties unchanged (`InvitedEmail`, `CreatedAtUtc` and `ExpiresAtUtc` are init; `Role` is settable). The prototype's settable properties become the input, and the factory sets the init values. `Derive` fills the *input*, in order, so expiry follows whatever creation time the test chose:

```csharp
public sealed class InvitationInput
{
    public string? Email { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string Role { get; set; } = "member";
}

public static class MembershipBlueprints
{
    public static readonly InputBlueprint<SeedContext, InvitationInput, OrganizationMembership> PendingInvitation =
        Blueprint
            .FromInput<SeedContext, InvitationInput, OrganizationMembership>(
                () => new(),
                (input, ctx) => new()
                {
                    Id = ctx.NewId("mem"),
                    OrganizationId = ctx.Seed.Organization.Id,
                    Role = input.Role,
                    Status = MembershipStatus.Pending,
                    InvitedEmail = input.Email!,
                    CreatedByUserId = ctx.Seed.Owner.Id,
                    CreatedAtUtc = input.CreatedAtUtc,
                    ExpiresAtUtc = input.ExpiresAtUtc,
                })
            .Derive(i => i.CreatedAtUtc, (_, ctx) => ctx.Now)
            .Derive(i => i.Email, (_, ctx) => $"{ctx.NewId("invite")}@example.test")
            .Derive(i => i.ExpiresAtUtc, (i, _) => i.CreatedAtUtc.AddDays(7));
}
```

```csharp
var now = DateTimeOffset.UtcNow;
await TestBiome.Start(_context, out var seed)
    .AddOne(MembershipBlueprints.PendingInvitation, out var expired, i =>
    {
        i.Email = "multi-invite@example.test";
        i.CreatedAtUtc = now.AddMinutes(-2);
        i.ExpiresAtUtc = now.AddDays(-1);
    })
    .AddOne(MembershipBlueprints.PendingInvitation, out var admin, i =>
    {
        i.Email = "multi-invite@example.test";
        i.Role = "admin";
    })
    .AddOne(MembershipBlueprints.PendingInvitation, out var input, i => i.Email = "create-input@example.test")
    .Transient()                                   // was PersistOnBuild = false
    .BuildAsync();
```

Notes:
- `now` here is the wall clock for brevity. Prefer `ctx.Now` in blueprints and a `(i, ctx) =>` customizer with `ctx.Ago(…)` in tests, or a shared `FakeTimeProvider`.
- `Derive` treats `default` as unset, so a test can't pick `default(DateTimeOffset)` or `null` on the input on purpose (a nullable value type keeps an explicit `0`/`false`, but not `null`).
- If these properties are already settable, a plain `Blueprint.For` with `.Derive(m => m.ExpiresAtUtc, (m, _) => m.CreatedAtUtc.AddDays(7))` is simpler and tests set `i => i.InvitedEmail = …` directly. Don't add setters to production models only for the tests.

## Step 4: `UserAliasPrototype` becomes a child of `User`

The prototype defaulted `User` to the seed owner and used `Kind` (init) as a discriminator:

```csharp
public static class IdentityBlueprints
{
    public static readonly ChildBlueprint<SeedContext, User, UserAlias> EmailAlias = AliasOf(UserAliasKind.Email);
    public static readonly ChildBlueprint<SeedContext, User, UserAlias> GitHubAlias = AliasOf(UserAliasKind.GitHub);

    private static ChildBlueprint<SeedContext, User, UserAlias> AliasOf(UserAliasKind kind) =>
        Blueprint.ChildOf<SeedContext, User, UserAlias>((user, ctx) => new()
            {
                Id = ctx.NewId("alias"),
                OrganizationId = ctx.Seed.Organization.Id,
                UserId = user.Id,
                Kind = kind,
                DisplayValue = kind == UserAliasKind.GitHub
                    ? ctx.NewId("github-alias")
                    : $"{ctx.NewId("alias")}@example.test",
                NormalizedValue = null!,                    // required + init: filled by Derive
                CreatedAtUtc = ctx.Now,
                UpdatedAtUtc = ctx.Now,
            })
            .Derive(a => a.NormalizedValue, (a, _) => a.DisplayValue.Trim().TrimStart('@').ToLowerInvariant());
}
```

```csharp
await TestBiome.Start(_context, out var seed)
    .Under(seed.Owner)                                      // was: prototype.User ?? seed.Owner (allowed: GetSeedEntities() lists Users)
    .AddOne(IdentityBlueprints.GitHubAlias, out var alias, a => a.DisplayValue = "@Octocat")
    .BuildAsync();
// alias.NormalizedValue == "octocat"
```

## Step 5: `FindLatest` chains become child blueprints

Before, `AddPullRequestRecords` / `AddCodeReviewRecords` / `AddActiveCodeReviewLocks` each called `FindLatest<…>(builder.Entities)` for their parents, and numbered `index + 1` per call. After:

```csharp
public static class CodeReviewBlueprints
{
    public static readonly Sequence<int> PullRequestNumbers = Sequence.Of(1, n => n + 1).Per<CodeRepository>();

    public static readonly Blueprint<SeedContext, CodeRepository> Repository =
        Blueprint.For<SeedContext, CodeRepository>(ctx =>
            {
                var id = ctx.NewId("repo");
                return new()
                {
                    Id = id,
                    OrganizationId = ctx.Seed.Organization.Id,
                    Provider = "github",
                    OwnerQualifiedName = $"zeeq-ai/{id}",
                    DisplayName = null!,                    // derived below
                    Enabled = true,
                    ReviewConfiguration = new(),            // fresh per entity: no shared Empty instance
                    CreatedAtUtc = ctx.Now,
                    UpdatedAtUtc = ctx.Now,
                };
            })
            .Derive(r => r.DisplayName, (r, _) => r.OwnerQualifiedName);

    public static readonly ChildBlueprint<SeedContext, CodeRepository, PullRequestRecord> PullRequest =
        Blueprint.ChildOf<SeedContext, CodeRepository, PullRequestRecord>((repo, ctx) => new()
            {
                Id = ctx.NewId("pr"),
                OrganizationId = repo.OrganizationId,
                RepositoryId = repo.Id,
                OwnerQualifiedRepoName = repo.OwnerQualifiedName,
                GitHubNodeId = null!,
                Title = null!,
                Branch = null!,
                HtmlUrl = null!,
                BaseBranch = "main",
                HeadSha = Guid.CreateVersion7().ToString("N"),
                AuthorLogin = "octocat",
                State = PullRequestState.Open,
                CreatedAtUtc = ctx.Now,
                CreatedFromWebhookAtUtc = ctx.Now,
                LastWebhookAtUtc = ctx.Now,
                UpdatedAtUtc = ctx.Now,
            })
            .Derive(pr => pr.PullRequestNumber, PullRequestNumbers)        // init: Derive can fill it
            .Derive(pr => pr.Title, (pr, _) => $"Pull request {pr.PullRequestNumber}")
            .Derive(pr => pr.Branch, (pr, _) => $"feature/{pr.PullRequestNumber}")
            .Derive(pr => pr.HtmlUrl, (pr, ctx) =>
                $"https://github.com/{ctx.Parent.OwnerQualifiedName}/pull/{pr.PullRequestNumber}")
            .Derive(pr => pr.GitHubNodeId, (_, ctx) => ctx.NewId("PR"));    // was "PR_" + number: collides across repos

    public static readonly ChildBlueprint<SeedContext, PullRequestRecord, CodeReviewRecord> Review =
        Blueprint.ChildOf<SeedContext, PullRequestRecord, CodeReviewRecord>((pr, ctx) => new()
        {
            Id = ctx.NewId("cr"),
            OrganizationId = pr.OrganizationId,
            PullRequestRecordId = pr.Id,
            RepositoryId = ctx.Ancestor<CodeRepository>().Id,             // was FindLatest<CodeRepository>
            OwnerQualifiedRepoName = pr.OwnerQualifiedRepoName,
            PullRequestNumber = pr.PullRequestNumber,
            Branch = pr.Branch,
            Title = pr.Title,
            AuthorLogin = pr.AuthorLogin,
            Status = CodeReviewStatus.Pending,
            CreatedAtUtc = ctx.Now,
            UpdatedAtUtc = ctx.Now,
        });

    public static readonly ChildBlueprint<SeedContext, CodeReviewRecord, ActiveCodeReviewLock> ActiveLock =
        Blueprint.ChildOf<SeedContext, CodeReviewRecord, ActiveCodeReviewLock>((review, ctx) =>
        {
            var pr = ctx.Ancestor<PullRequestRecord>();
            return new()
            {
                OrganizationId = review.OrganizationId,
                RepositoryId = pr.RepositoryId,
                PullRequestRecordId = pr.Id,
                PullRequestCreatedAtUtc = pr.CreatedAtUtc,
                CodeReviewRecordId = review.Id,
                CodeReviewCreatedAtUtc = review.CreatedAtUtc,
                Status = review.Status,
                AcquiredAtUtc = review.CreatedAtUtc,
                ExpiresAtUtc = ctx.FromNow(TimeSpan.FromMinutes(4)),
                UpdatedAtUtc = review.CreatedAtUtc,
            };
        });
}
```

Notes:
- The child factory reads values from its parent (`pr.Branch`, `pr.Title`). Those are already final, because the parent's `Derive`s ran when the parent's own step completed.
- An explicit `pr => pr.PullRequestNumber = 10` isn't possible while `PullRequestNumber` is `init`. When tests need to choose the number, make `PullRequestRecord` a typed child input instead: `Blueprint.ChildFromInput<SeedContext, CodeRepository, PullRequestInput, PullRequestRecord>` with `PullRequestInput.Number`, `.Derive(i => i.Number, PullRequestNumbers)` on the input, and `PullRequestNumber = input.Number` in the factory (step 3 shows the pattern). The sequence skips explicit numbers across the whole step.

Then the four sugar one-liners per type (see `authoring.md`), plus the helper the store tests repeat:

```csharp
public static class CodeReviewSugar
{
    extension(BiomeBuilder<SeedContext, SeedContext> b)
    {
        public BiomeBuilder<SeedContext, ActiveCodeReviewLock[]> AddLockedReview(out ActiveCodeReviewLock activeLock) =>
            b.AddOne(CodeReviewBlueprints.Repository, out _)
                .AddOne(CodeReviewBlueprints.PullRequest, out _)
                .AddOne(CodeReviewBlueprints.Review, out _)
                .AddOne(CodeReviewBlueprints.ActiveLock, out activeLock);
    }
}
```

## Step 6: call sites

Before (`CodeReviewStoreIntegrationTests.cs`):

```text
var (_, _, _, _, activeLocks) = await EntityGraph
    .AddGeneratedSeed(_context)
    .AddCodeRepository()
    .AddPullRequestRecords()
    .AddCodeReviewRecords()
    .AddActiveCodeReviewLocks()
    .BuildAsync();
var activeLock = activeLocks[0];
```

After:

```csharp
await TestBiome.Start(_context, out _).AddLockedReview(out var activeLock).BuildAsync();
_context.ChangeTracker.Clear();
```

Mechanical rewrites:
- `var (seed, x) = await EntityGraph.AddGeneratedSeed(_context).AddX(…).BuildAsync();` becomes `await TestBiome.Start(_context, out var seed).AddX(out var x, …).BuildAsync();`.
- Tuple discards `_` become `out _`, and `xs[0]` becomes singular sugar.
- `.AddX(p => p.PersistOnBuild = false)` becomes `.AddX(out var x).Transient()`.
- A prototype customizer `p => p.Prop = v` becomes `e => e.Prop = v` on the entity if `Prop` is settable, or `i => i.Prop = v` on an input blueprint's input if the entity keeps `init` (see step 2).
- `EntityGraph.SeedWith(seed, _context).Push(existing)` has no direct equivalent: build the entity through a blueprint. `.Under(x)` only accepts Add outputs and seed rows selected by `SeedEntities`, so a hand-built object can't be a parent.

## Step 7: aggregates (`OrganizationGraph`)

Keep `OrganizationPrototype` as the **input**, and implement `IBiomeAggregate` on the graph. It already has `GetEntities()`:

```csharp
public sealed record OrganizationGraph(
    Organization Organization, Team RootTeam, OrganizationMembership OrganizationMembership, TeamMembership RootTeamMembership)
    : IBiomeAggregate
{
    public IEnumerable<object> GetEntities() => [Organization, RootTeam, OrganizationMembership, RootTeamMembership];
}

public static class OrganizationBlueprints
{
    public static readonly InputBlueprint<SeedContext, OrganizationPrototype, OrganizationGraph> Organization =
        Blueprint.FromInput<SeedContext, OrganizationPrototype, OrganizationGraph>(
            () => new(),
            (p, ctx) =>
            {
                var owner = p.Owner ?? ctx.Seed.Owner;
                var now = p.CreatedAtUtc ?? ctx.Now;
                var org = new Organization
                {
                    Id = ctx.NewId("org"),
                    DisplayName = p.DisplayName,
                    Slug = p.Slug,
                    CreatedByUserId = owner.Id,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    ActivatedAtUtc = p.IsActivated ? p.ActivatedAtUtc ?? now : null,
                };
                var team = new Team
                {
                    Id = ctx.NewId("team"), OrganizationId = org.Id, DisplayName = "Root Team",
                    IsRootTeam = true, CreatedByUserId = owner.Id, CreatedAtUtc = now, UpdatedAtUtc = now,
                };
                var membership = new OrganizationMembership
                {
                    Id = ctx.NewId("mem"), OrganizationId = org.Id, UserId = owner.Id, Role = p.Role,
                    Status = MembershipStatus.Active, IsDefault = p.IsDefaultMembership,
                    CreatedByUserId = owner.Id, CreatedAtUtc = now,
                };
                var teamMembership = new TeamMembership
                {
                    OrganizationId = org.Id, TeamId = team.Id, UserId = owner.Id, Role = p.Role,
                    CreatedByUserId = owner.Id, CreatedAtUtc = now,
                };
                return new(org, team, membership, teamMembership);
            })
        .Derive(p => p.Slug, (_, ctx) => $"test-organization-{ctx.NewId("org")[^8..]}");
}
```

```csharp
await TestBiome.Start(_context, out var seed)
    .Add(OrganizationBlueprints.Organization, out var graphs,
        o => { o.Slug = "graph-org-a"; o.IsDefaultMembership = true; },
        o => { o.Slug = "graph-org-b"; o.IsDefaultMembership = false; })
    .BuildAsync();
```

Inputs have settable properties, so they don't hit the `init` problem. Global `Default`s apply to the graph object, not to its rows. The same input pattern works for a single immutable entity (step 3).

## Step 8: delete the old pieces

Once a type's call sites have moved, delete:
- its `*Prototype` class and its `Create*` method;
- its `EntityGraphBuilder<(TState Previous, …)>` overloads;
- the `PersistOnBuild` plumbing.

Keep `EntityGraph` and Biome side by side until the last call site has moved. They don't share state.
