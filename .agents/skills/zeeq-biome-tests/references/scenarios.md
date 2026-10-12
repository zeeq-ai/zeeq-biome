# Zeeq Biome test scenarios

Worked setups for common test shapes. They use the README's example domain: `ZeeqSeed`, the `Blueprints` catalog, generated grafts `AddCodeRepository` / `AddPullRequest` / `AddCodeReview` / `AddActiveLock` / `AddUser(s)`, `AddLockedReview`, and the `PullRequests` traits. Use the graft names and scenario helpers your app defines.

## 1. A store test that needs a deep chain

Before (zeeq-app `EntityGraph`; shown for comparison, not Biome code):

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
await TestBiome.Start(_db, out _).AddLockedReview(out var activeLock).BuildAsync();
_db.ChangeTracker.Clear();
// act: await new PostgresActiveCodeReviewLockStore(_db).RefreshAsync(activeLock.OrganizationId, …);
```

If the test also asserts on the PR or the review, spell out the chain and name only those:

```csharp
await TestBiome.Start(_db, out _)
    .AddCodeRepository(out _)
    .AddPullRequest(out var pr)
    .AddCodeReview(out var review)
    .AddActiveLock(out var activeLock)
    .BuildAsync();
```

## 2. Siblings and branches

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .AddPullRequest(out var open)                          // #1
    .Under(repo)
    .AddPullRequest(out var closed, PullRequests.Closed)   // #2, sibling of `open`
    .AddCodeReview(out var review)                         // reviews `closed`
    .Under(open)
    .AddCodeReview(out var otherReview)                    // reviews `open`
    .BuildAsync();
```

`.Under` accepts one entity or an array (`.Under(repos)` fans out across all of them). The entity must be an Add output or a seed row from `SeedEntities`, such as `.Under(seed.Owner)`.

## 3. Fan-out across several parents

```csharp
await TestBiome.Start(out _)
    .AddCodeRepositories(out var repos, 2)
    .AddPullRequests(out var prs, PullRequests.Draft, PullRequests.Merged)   // 2 per repo → 4
    .BuildAsync();
// prs[0], prs[1] → repos[0] (#1 draft, #2 merged); prs[2], prs[3] → repos[1] (#1, #2)
```

A customizer that needs the parent uses the two-parameter form and the core `Add`:

```csharp
await TestBiome.Start(out _)
    .AddUsers(out var users, u => u.DisplayName = "Ada", u => u.DisplayName = "Grace")
    .Add(Blueprints.UserAlias, out var aliases,
        (alias, ctx) => alias.DisplayValue = $"@{ctx.Parent.DisplayName}")
    .BuildAsync();
```

`ctx.Index` is the position under the current parent, and `ctx.ParentIndex` is the parent's position in the anchor array. Use them only for cosmetic values. Uniqueness comes from sequences or `ctx.NewId`.

## 4. A specific number or position

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .AddPullRequests(out var prs,
        _ => { },                                 // #2
        pr => pr.PullRequestNumber = 1,           // #1 (explicit; reserved first)
        _ => { })                                 // #3
    .Under(repo)
    .AddPullRequest(out var next)                 // #4
    .BuildAsync();
```

- `_ => { }` means "one default entity" inside a customizer list.
- An explicit value set in a **later** step can't move numbers that are already assigned. Set explicit values first.

## 5. An input the code under test inserts

```csharp
await TestBiome.Start(_db, out _)
    .AddOne(Blueprints.PendingInvitation, out var invitation, i => i.InvitedEmail = "new@example.test")
    .Transient()
    .BuildAsync();
// act: await invitationStore.CreateAsync(invitation);   // inserts it; the biome did not
```

## 6. Time

The default clock is frozen at `Start`, so everything in the scenario shares one `Now`. To move time, share a provider with the code under test:

```csharp
var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
await TestBiome.Start(_db, out _, time)
    .AddLockedReview(out var activeLock)          // ExpiresAtUtc = 12:05 (blueprint: FromNow(5 min))
    .BuildAsync();

time.Advance(TimeSpan.FromMinutes(6));
// act: var expired = await new LockSweeper(_db, time).SweepAsync();   // same clock
```

To set a time relative to now, use the context-aware form, which works with any clock:

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out _)
    .AddPullRequest(out _)
    .AddOne(Blueprints.Review, out var stale, (r, ctx) => r.CreatedAtUtc = ctx.Ago(TimeSpan.FromHours(2)))
    .BuildAsync();
```

Values are rounded to 1 µs (Postgres precision), so a value read back from Postgres equals the value in memory. Don't compare against `DateTimeOffset.UtcNow`.

## 7. Inputs and aggregates (customize an input, build the row)

For a blueprint made with `FromInput` / `ChildFromInput`, the customizer sets properties of the **input**, not of the row:

```csharp
await TestBiome.Start(_db, out _)
    .Add(Blueprints.Organization, out var orgs,
        o => o.IsDefaultMembership = true,
        o => o.Slug = "beta")
    .BuildAsync();
// orgs[0].Organization, orgs[0].RootTeam, orgs[0].Membership are all saved
```

A child built from an input fans out per parent like any child, and `out` holds the row (not the input):

```csharp
await TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .Add(Blueprints.PullRequestFromInput, out var prs, 2)               // numbers 1, 2
    .Under(repo)
    .AddOne(Blueprints.PullRequestFromInput, out var pr10, i => i.Number = 10)
    .BuildAsync();
```

Inputs aren't rows: `Under`, `Transient()`, `All<T>()` and `Last<T>()` work on the outputs, and `All<Input>()` is empty. Ordinary-entity inputs and `ChildFromInput` require a referenced package that exposes those forms; check the package API without inferring a public version from this branch.

## 8. A pure unit test (no database)

```csharp
TestBiome.Start(out _)
    .AddCodeRepository(out var repo)
    .AddPullRequests(out var prs, PullRequests.Draft, PullRequests.Merged, PullRequests.Closed);
// act: var summary = PullRequestSummary.From(repo, prs);
```

Without a store nothing is saved, so `BuildAsync` is optional. Call it if save hooks matter to the test.

## 9. Inspecting what was built

Keep the starting builder when you need to inspect entities you didn't name:

```csharp
var biome = TestBiome.Start(out _);
await biome.AddLockedReview(out var activeLock).BuildAsync();

var review = biome.Last<CodeReviewRecord>();    // created by the helper
var repos = biome.All<CodeRepository>();        // creation order; includes transients; excludes seed rows
```

Prefer naming entities with `out` when the test is about them. `All` / `Last` are for entities that helpers create.
