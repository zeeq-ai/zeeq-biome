---
name: zeeq-biome-blueprints
description: Use when creating or changing Zeeq.Biome test infrastructure. That covers the seed type, the `TestBiome` entry point, blueprints (`Blueprint.For` / `ChildOf` / `FromInput`), traits and variants (`.With`), `.Derive`, sequences, `.AsTransient`, the four `AddX`/`AddXs` sugar methods, scenario helpers, `BiomeOptions` (`SeedEntities`, `Default`, `BeforeSave`/`AfterSave`, `TimePrecision`, `Ids`), and EF Core store wiring. Also use when migrating zeeq-app `EntityGraph` extensions or `*Prototype` classes to Biome, or when a new entity type needs test support. For writing test chains with existing sugar, use zeeq-biome-tests.
---

# Authoring Zeeq Biome blueprints

- Templates and API detail: `.agents/skills/zeeq-biome-blueprints/references/authoring.md`.
- Migrating from zeeq-app `EntityGraph`: `.agents/skills/zeeq-biome-blueprints/references/migration-from-entitygraph.md`.
- The public API is in `src/Zeeq.Biome/` and living examples are in `tests/Zeeq.Biome.Tests/` (`CodeReviews/`, `Membership/`, `Board/`, `CustomSeed/`).

## What to write, per entity type

1. **One blueprint**, a `static readonly` value in a `Blueprints` class for that area. A root type uses `Blueprint.For<TSeed, T>(ctx => new() { … })`. A type that belongs to a parent uses `Blueprint.ChildOf<TSeed, TParent, T>((parent, ctx) => new() { … })`.
2. **Four sugar one-liners** in a C# 14 `extension` block: singular default, singular with `Action<T>`, plural with a count, plural with `params Action<T>[]`.
   - Root sugar goes on `extension<TAnchor>(BiomeBuilder<TSeed, TAnchor> b)`.
   - Child sugar goes on `extension(BiomeBuilder<TSeed, TParent[]> b)`.
3. **Optionally:** named traits (`static readonly Action<T>`), variants (`bp.With(trait)`), and a scenario helper for chains that tests repeat.

## Factory rules

- Produce a **valid, uninteresting** entity. Every required column gets a default that no test should care about.
- IDs come from `ctx.NewId("prefix")`. Time comes from `ctx.Now` / `ctx.Ago` / `ctx.FromNow`, **never** `DateTimeOffset.UtcNow`.
- Take the parent from the typed argument and grandparents from `ctx.Ancestor<T>()`. Never use `ctx.Last<T>()` or `All<T>()` to wire relationships.
- **Leave derived properties unset** (`default`) so `Derive` can fill them. For `required` members, assign `null!` / `0` in the factory and let `Derive` fill them.
- Entities and parents must be **reference types**. The seed can be anything.
- A property that tests vary must be **settable**. Customizers can't assign `init` properties (CS8852). See the migration guide for options when the model uses `init`.
- `ctx.Index` is the position in the current Add (per parent for children). It's for cosmetic values only, never for uniqueness or order.

## Derive, sequences, transients

- `.Derive(e => e.Prop, (e, ctx) => value)` is **fill-only**: it assigns only if the property still equals `default(TProp)` after traits and customizers. `""` counts as set. A `Derive` can't keep an explicit `null`.
- `Derive`s run in declaration order, so derive from a sequence value after the sequence. `.Derive(pr => pr.Title, (pr, _) => $"PR {pr.PullRequestNumber}")` goes after the number.
- `Sequence.Of(start, next)` is declared once, as a static value. Scope it with `.PerSession()` (the default), `.PerParent()`, or `.Per<TAncestor>()`, and assign it with `.Derive(e => e.Prop, sequence)`. Explicit values are reserved and skipped.
- Globally unique columns (node IDs, slugs, emails) use `ctx.NewId` or a `PerSession` sequence, **not** a `PerParent` / `Per<T>` one.
- Prefer `Derive(prop, sequence)` over `ctx.Next(sequence)` in a factory. `ctx.Next` runs before customizers, so an explicit value would never be reserved.
- `.AsTransient()` makes a blueprint whose output is never saved. Tests use `.Transient()` for a single step.

## Variants, traits, inputs

- A trait is an `Action<T>` (or `Action<T, BiomeContext<TSeed>>`). Pass it as a customizer, or bake it in with `bp.With(trait)`. Blueprints are immutable, so `With` returns a new value.
- Declare variants **below their base in the same file**. Static fields initialize top to bottom, and across `partial` files the order is unspecified. The same goes for sequences used by a blueprint.
- `Blueprint.FromInput<TSeed, TInput, TAggregate>(() => new(), (input, ctx) => …)` is **only for aggregates**: one concept saved as several rows. The output implements `IBiomeAggregate.GetEntities()`, which returns the rows in save order. Customizers act on `TInput`. Parts aren't tracked as parents, so child blueprints of an aggregate take the aggregate type as `TParent`.

## Scenario helpers

These are named, app-level chains, declared on `extension(BiomeBuilder<TSeed, TSeed> b)` so they're callable only from the seed. They return the leaf:

```csharp
public BiomeBuilder<ZeeqSeed, ActiveCodeReviewLock[]> AddLockedReview(out ActiveCodeReviewLock activeLock) =>
    b.AddCodeRepository(out _).AddPullRequest(out _).AddCodeReview(out _).AddActiveLock(out activeLock);
```

Biome has no hidden ancestor creation. Each helper's whole chain is visible in its definition.

## Options and saving

- Configure options once, as a `static readonly BiomeOptions<TSeed>` in `TestBiome`. They freeze at the first `Biome.Start` and are shared by parallel tests.
- `SeedEntities(seed => […])` returns the seed rows in foreign-key-safe order. They are saved before everything else.
- `Default<TBase, TProp>(e => e.Prop, (e, ctx) => …)` is a fill-only default for every entity matching a class or interface. It runs after blueprint `Derive`s. The interface property needs a setter. It doesn't reach aggregate parts or rows added in `BeforeSave`.
- `BeforeSave` / `AfterSave((save, ct) => …)`: `save.Entities` is the mutable row list, alongside `save.Seed` and `save.Session`. Rows added there skip traits, `Derive`s, and global defaults. Use these hooks sparingly, because rows a test can't see make it harder to read.
- `TimePrecision(TimeSpan)` defaults to 1 µs; `TimeSpan.Zero` turns rounding off. `Ids(prefix => …)` overrides `ctx.NewId`.
- EF Core: pass `new EfCoreStore(db)` (or `db.AsBiomeStore()`; package `Zeeq.Biome.EntityFrameworkCore`) as the store in a `TestBiome.Start(DbContext db, out var seed, TimeProvider? time = null)` overload. For database-generated keys, set navigation properties in child factories. EF also saves anything reachable through a navigation property, including transient entities.
- `SeedEntities` decides which seed objects tests can anchor on with `.Under(seed.X)`. A seed object that isn't listed can't be a parent.

## Mistakes to avoid

- **Setting a derived property in the factory.** `Derive` then never fires, because the value already looks "set".
- **Wiring relationships with `ctx.Last<T>()`.** That's zeeq-app's old `FindLatest`, and it breaks with siblings and `.Under`. Use a child blueprint and `ctx.Ancestor<T>()`.
- **Numbering with `ctx.Index + 1`.** It restarts on every Add call, which caused zeeq-app's duplicate PR #1. Use a sequence.
- **A `PerParent` sequence for a value that must be globally unique.** Two parents both produce `PR_1`.
- **Mixing `Action<T>` and `Action<T, ctx>` lambdas in one call.** C# can't. Sugar exposes `Action<T>`, and tests that need `ctx` use the core `Add` / `AddOne`.
- **Adding ctx overloads, or overloads without `out`, to sugar.** Keep it to four. Callers use `out _`.
- **Ancestor creation inside a child sugar method.** Write a named scenario helper instead.
- **An `InputBlueprint` for a single entity.** Customize the entity directly.
- **Changing options after the first `Start`.** It throws. Build test-specific options with a factory method instead (see `tests/Zeeq.Biome.Tests/CustomSeed/Domain.cs`).
