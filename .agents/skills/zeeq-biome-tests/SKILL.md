---
name: zeeq-biome-tests
description: Use when writing, reading, or fixing test setup that uses Zeeq.Biome. That means `TestBiome.Start(...)` chains, `AddX(out var x)` / `AddXs(out var xs, …)` sugar, `.Under(...)`, `.Transient()`, sequences, or `FakeTimeProvider` in a test. Also use when a test's setup fails to compile (a missing `AddX` after a step, an overload or lambda mismatch) or fails at runtime with a Biome exception, duplicate keys, or timestamp mismatches. For creating seeds, blueprints, sugar methods, or options, use zeeq-biome-blueprints instead.
---

# Writing test scenarios with Zeeq Biome

Longer worked scenarios: `.agents/skills/zeeq-biome-tests/references/scenarios.md`. Library overview: the repo `README.md`.

A test's setup is **one fluent chain**. It names only the entities the test asserts on, and sets only the properties under test. Everything else comes from blueprint defaults.

```csharp
await TestBiome.Start(_db, out var seed)
    .AddCodeRepository(out _)
    .AddPullRequest(out var pr, p => p.State = PullRequestState.Closed)
    .AddCodeReview(out var review)
    .BuildAsync();
```

## Reading a chain

- `TestBiome.Start(...)` begins a new session (one per test). The anchor is the **seed**.
- **Every Add re-anchors to its own output.** Read the chain top to bottom: each child belongs to the line above it.
- `.Under(x)` jumps back to an earlier Add output, or a seed row. The next Add creates a sibling, or starts another branch.
- Entities are **created immediately** at each Add, so `out` variables are usable right away. `BuildAsync()` saves everything in one go.
- `.Transient()` excludes the step just above it from the save. The entities are still returned.

## Writing a chain

1. **Start** with `TestBiome.Start(_db, out var seed)` when you need the database, `TestBiome.Start(out var seed)` when you don't, and add `time` when the clock matters.
2. **Prefer singular sugar** (`AddPullRequest(out var pr)` returns `T`). Use plural (`AddPullRequests(out var prs, 3)`) only when the test needs several. Never write `prs[0]` to get a single entity.
3. **`out _` for anything you won't assert on.** Every Add needs an `out`.
4. **Set only the property under test.** Never restate a default. Use named traits when the app has them (`PullRequests.Closed`).
5. **Comma = a new entity. `+` = stack traits on the same entity.** `AddPullRequests(out var prs, PullRequests.Closed, PullRequests.Closed + PullRequests.Draft)` creates 2 PRs.
6. **Children fan out per parent**, so 2 repositories × 3 customizers = 6 PRs. The result is one flat array in parent order: `prs[0..2]` belong to `repos[0]`.
7. **Siblings:** `.Under(repo).AddPullRequest(out var second)`. **Grandparents** are wired by the blueprints. Don't pass them in yourself.
8. **Inputs the code under test should insert:** build them, then call `.Transient()` straight after that step.
9. **Numbers and ordering come from sequences.** Don't hand-number. If a test needs a specific number, set it in the same step as the defaults, or an earlier one. The sequence then skips it.
10. **Time:** never use `DateTimeOffset.UtcNow` in setup. Pass a `FakeTimeProvider` to both `Start(…, time)` and the system under test. For relative times, use the context-aware form, `(r, ctx) => r.CreatedAtUtc = ctx.Ago(TimeSpan.FromMinutes(4))`.
11. **Save once.** Call `BuildAsync()` once, at the end of setup. With EF, call `_db.ChangeTracker.Clear()` before the code under test reads, so it gets database rows rather than tracked instances.
12. **No database:** skip the store. `BuildAsync()` is then optional, because entities exist as soon as each Add returns.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `CS1929: 'BiomeBuilder<ZeeqSeed, X[]>' does not contain a definition for 'AddY' … requires a receiver of type 'BiomeBuilder<ZeeqSeed, Parent[]>'` | The anchor isn't Y's parent type. Child sugar exists only after its parent's step. | Add the parent first, or `.Under(parent)`. Use a scenario helper (`AddLockedReview`) if there is one. |
| `CS1503: cannot convert from 'lambda expression' to 'out X'` | Missing `out`. Every Add takes one first. | `AddX(out _, x => …)` |
| `CS1593: Delegate 'Action<X>' does not take 2 arguments` | Either `x => …` and `(x, ctx) => …` were mixed in one call (C# can't), or a `(x, ctx) =>` lambda was passed to app sugar that has no context overload (sugar takes `Action<T>` unless the app added one for that type). | Write every customizer in the `(x, ctx) =>` form and call the core API: `.AddOne(Blueprints.X, out var x, (x, ctx) => …)` / `.Add(Blueprints.X, out var xs, …)`. |
| `CS8852: Init-only property … can only be assigned in an object initializer` | The entity property is `init`, so a customizer can't set it. | Ask the blueprint author for an input blueprint so the test customizes an input and the factory builds the row. Don't change the production property to `set` just for the test (see zeeq-biome-blueprints). |
| `Parent at index N of type X is not owned by this biome session` | `.Under(...)` was given an object this session didn't create: something built by hand, an aggregate part, or an entity from another test. | Use an `out` from an Add, or a seed row listed in `SeedEntities`. For aggregates, anchor on the aggregate itself. |
| An Add throws, and then every later call throws with it as the inner exception | A factory, trait, customizer or `Derive` threw, which faults the session. | Fix the first exception, the inner one. Don't catch it and continue; use a new `Start`. |
| A `.Transient()` row was saved anyway (EF) | A saved row references it through a navigation property, and EF follows the reference. | Don't link persisted rows to transient inputs, or mark the referencing step transient too. |
| `A singular child AddOne requires exactly one parent` | The previous step produced several (or zero) parents. | Use the plural Add, or `.Under(repos[0])`. |
| `No ancestor of type X exists…` | A blueprint walks up to X, but the chain skipped X. | Build the full chain (repo → PR → review), or use a scenario helper. |
| `BuildAsync may only be called once…` / `already started BuildAsync…` | Two builds, or an Add after the build. | Put all setup before one `BuildAsync()`. Use a second `Start` only for a genuinely separate scenario. |
| `Biome options are frozen…` | Test code changed shared options. | Options are configured once in `TestBiome`. Never change them in a test. |
| Duplicate key or a unique violation on a number | An explicit value was set in a **later** step than the defaults that already used it. | Set explicit values in the same step as the defaults, or an earlier one. |
| A timestamp differs by microseconds or ticks after reading back | `DateTimeOffset.UtcNow` was used in the test. Biome values are rounded to 1 µs. | Use `ctx.Now` / `ctx.Ago`, or a `FakeTimeProvider` shared with the code under test. |
| An "explicit `null`" or `0` was replaced | `Derive` and defaults treat `default(T)` as unset. | You can't keep `null` through a `Derive`. Ask for a base blueprint without that `Derive`. `""` does count as set. |
| `.Transient()` marked the wrong rows | It marks the session's **most recent Add step**, not the builder you call it on. | Put `.Transient()` immediately after the step it is meant to mark. |
| `biome.All<T>()` count includes unexpected rows | `All` / `Last` include transient entities and aggregate wrappers, but not seed rows. | Assert on `out` variables instead. |

## Checklist

- [ ] One `Start` and one `BuildAsync` per scenario.
- [ ] Only asserted entities are named. Everything else is `out _`.
- [ ] Each customizer sets only properties the test depends on.
- [ ] No `[0]` indexing for single entities. Use singular sugar.
- [ ] No `DateTimeOffset.UtcNow` or hand-written numbers in setup. The clock, if any, is shared with the code under test.
- [ ] Inputs that the code under test inserts are `.Transient()`.
- [ ] With EF: `_db.ChangeTracker.Clear()` after `BuildAsync()` when the code under test reads.
