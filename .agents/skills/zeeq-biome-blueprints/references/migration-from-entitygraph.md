# Migrating from zeeq-app `EntityGraph`

The source is `zeeq-app/src/backend/Zeeq.Testing/EntityGraph/`: `EntityGraphBuilder.cs`, `SeedContext.cs`, and `EntityGraph*Extensions.cs`. The "after" code below targets zeeq-app's real `Zeeq.Core.Models` types. "Before" snippets are the old API and are marked `text`.

## Concept map

| EntityGraph | Biome |
|---|---|
| `EntityGraph.AddGeneratedSeed(_context)` / `SeedContext.Generate()` | `TestBiome.Start(_context, out var seed)`. `SeedContext` itself can be the seed type. |
| `*Prototype` class + `Create*` method + 2–3 overloads | one blueprint + `Derive`s + the four sugar one-liners |
| `prototype.X ?? default` | factory default, or fill-only `.Derive(e => e.X, …)` |
| `PersistOnBuild = false` | `.Transient()` after the step, or `bp.AsTransient()` |
| `FindLatest<CodeRepository>(builder.Entities)` | a child blueprint's typed parent, plus `ctx.Ancestor<CodeRepository>()` |
| `PullRequestNumber = index + 1` (restarts every call) | `Sequence.Of(1, n => n + 1).Per<CodeRepository>()` + `.Derive(pr => pr.PullRequestNumber, seq)` |
| `var (_, _, _, _, locks) = await … .BuildAsync(); var l = locks[0];` | `.AddActiveLock(out var l)`; `BuildAsync()` returns nothing |
| `OrganizationGraph` + `OrganizationPrototype` | `InputBlueprint` (the prototype becomes the input) and `IBiomeAggregate` |
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

## Step 2: first, check whether properties are `init`

zeeq-app entities make many properties `init`. Examples:
- `DomainEntityBase.CreatedAtUtc`
- `PullRequestRecord.PullRequestNumber`
- `UserAlias.Kind` and `UserAlias.NormalizedValue`
- `OrganizationMembership.InvitedEmail`, `ExpiresAtUtc` and `UserId`
- `ActiveCodeReviewLock.AcquiredAtUtc`

**A customizer can't assign an `init` property (CS8852)**, and prototypes hid this. Pick one approach per property:

| Situation | Approach |
|---|---|
| Tests vary it, and the domain allows it | **Preferred:** change it to `{ get; set; }` (EF maps both the same way). Confirm with the model's owner. |
| Tests never vary it | Set it in the factory. `Derive` can also fill `init` properties, since it uses a compiled setter. |
| It's a `required` init property derived from others | Assign `null!` / `default` in the factory, then `.Derive(...)`. |
| It's a discriminator (an enum like `UserAliasKind`) | Write one blueprint per value with a private factory method (see step 4). |
| Tests vary it, and it must stay `init` | Write a blueprint **factory method** with optional named parameters (see step 3). |

## Step 3: a prototype with derived defaults (`PendingInvitationPrototype`)

Before (shape):

```text
var createdAt = prototype.CreatedAtUtc ?? now;
… InvitedEmail = prototype.Email ?? $"{SeedContext.NewId("invite")}@example.test",
  ExpiresAtUtc = prototype.ExpiresAtUtc ?? createdAt.AddDays(7),
… if (!prototype.PersistOnBuild) nonPersistentInvitations.Add(invitation);
```

After, with today's `init` model (`InvitedEmail`, `CreatedAtUtc` and `ExpiresAtUtc` are init; `Role` is settable):

```csharp
public static class MembershipBlueprints
{
    public static Blueprint<SeedContext, OrganizationMembership> PendingInvitation(
        string? email = null, DateTimeOffset? createdAtUtc = null, DateTimeOffset? expiresAtUtc = null) =>
        Blueprint.For<SeedContext, OrganizationMembership>(ctx =>
        {
            var createdAt = createdAtUtc ?? ctx.Now;
            return new()
            {
                Id = ctx.NewId("mem"),
                OrganizationId = ctx.Seed.Organization.Id,
                Role = "member",
                Status = MembershipStatus.Pending,
                InvitedEmail = email ?? $"{ctx.NewId("invite")}@example.test",
                CreatedByUserId = ctx.Seed.Owner.Id,
                CreatedAtUtc = createdAt,
                ExpiresAtUtc = expiresAtUtc ?? createdAt.AddDays(7),
            };
        });
}
```

```csharp
var now = DateTimeOffset.UtcNow;
await TestBiome.Start(_context, out var seed)
    .AddOne(MembershipBlueprints.PendingInvitation(
        email: "multi-invite@example.test", createdAtUtc: now.AddMinutes(-2), expiresAtUtc: now.AddDays(-1)),
        out var expired)
    .AddOne(MembershipBlueprints.PendingInvitation(email: "multi-invite@example.test"),
        out var admin, i => i.Role = "admin")
    .AddOne(MembershipBlueprints.PendingInvitation(email: "create-input@example.test"), out var input)
    .Transient()                                   // was PersistOnBuild = false
    .BuildAsync();
```

Once those properties are settable, this collapses to the README form: a `static readonly` blueprint with `.Derive(m => m.ExpiresAtUtc, (m, _) => m.CreatedAtUtc.AddDays(7))`, and tests customize with `i => i.InvitedEmail = …`. Prefer that form. Factory methods create a new blueprint on every call, and any `Derive` on them recompiles its setter each time.

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
- An explicit `pr => pr.PullRequestNumber = 10` isn't possible while `PullRequestNumber` is `init`. Make it settable (preferred), or add a factory method as in step 3.

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
- A prototype customizer `p => p.Prop = v` becomes `e => e.Prop = v` on the entity, if `Prop` is settable (see step 2).
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

Inputs have settable properties, so they don't hit the `init` problem. Global `Default`s apply to the graph object, not to its rows.

## Step 8: delete the old pieces

Once a type's call sites have moved, delete:
- its `*Prototype` class and its `Create*` method;
- its `EntityGraphBuilder<(TState Previous, …)>` overloads;
- the `PersistOnBuild` plumbing.

Keep `EntityGraph` and Biome side by side until the last call site has moved. They don't share state.
