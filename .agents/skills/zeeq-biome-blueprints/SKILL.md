---
name: zeeq-biome-blueprints
description: Use when creating or changing Zeeq.Biome test infrastructure. That covers the seed type, the `TestBiome` entry point, blueprints (`Blueprint.For` / `ChildOf` / `FromInput` / `ChildFromInput`), traits and variants (`.With`), `.Derive`, sequences, `.AsTransient`, generated grafts (`[BiomeGraft]`, `[Graft]`, `[NoGraft]`), scenario helpers, `BiomeOptions` (`SeedEntities`, `Default`, `BeforeSave`/`AfterSave`, `TimePrecision`, `Ids`), and EF Core store wiring. Also use when migrating zeeq-app `EntityGraph` extensions or `*Prototype` classes to Biome, or when a new entity type needs test support. For writing test chains with existing grafts, use zeeq-biome-tests.
---

# Authoring Zeeq Biome blueprints

- Templates and API detail: `.agents/skills/zeeq-biome-blueprints/references/authoring.md`.
- Migrating from zeeq-app `EntityGraph`: `.agents/skills/zeeq-biome-blueprints/references/migration-from-entitygraph.md`.
- The public API is in `src/Zeeq.Biome/` and living examples are in `tests/Zeeq.Biome.Tests/` (`CodeReviews/`, `Membership/`, `Board/`, `CustomSeed/`).

## What to write, per entity type

1. **One blueprint**, a `static readonly` value in a `Blueprints` class for that area. A root type uses `Blueprint.For<TSeed, T>(ctx => new() { … })`. A type that belongs to a parent uses `Blueprint.ChildOf<TSeed, TParent, T>((parent, ctx) => new() { … })`.
2. **Generated grafts by default.** Put the blueprints in a top-level, non-generic, non-file-local `static class` marked `[BiomeGraft]`; `partial` is not required. The generator creates a sibling `{Catalog}Grafts` class. It reads public, internal, or protected-internal non-const `static readonly` fields and static getter-only properties with no setter whose types are one of the four blueprint types. Use `[Graft("Name", Plural = "Names")]` to override a method suffix, and `[NoGraft]` to exclude a member.
   - Root and root-input grafts are available from any anchor for the same seed.
   - Child and child-input grafts are available only from `BiomeBuilder<TSeed, TParent[]>` for their declared parent.
   - Each valid member gets three methods: singular `AddName(out T, Action<T>? customize = null)`, plural `AddNames(out T[], int count)`, and plural `AddNames(out T[], params Action<T>[] customize)`. For an input blueprint, use `TInput` in the customizer type while `out` and the new anchor hold output `T`.
   - Root grafts operate once from the current anchor; the count or customizer list sets the number of outputs. Child grafts fan out for each parent in the anchor; their count/customizers apply to each parent. An empty plural customizer list creates one default output per parent (or one output for a root).
3. **Write an `AddX` extension by hand only for a customization or exception case.** Grafts do not generate context-aware customizers or methods without `out`. For a one-off context-aware customizer, use core `Add` / `AddOne`; add a handwritten context overload only when tests repeatedly need it.
4. **Optionally:** named traits (`static readonly Action<T>`), variants (`bp.With(trait)`), and a scenario helper for chains that tests repeat.

## Factory rules

- Produce a **valid, uninteresting** entity. Every required column gets a default that no test should care about.
- IDs come from `ctx.NewId("prefix")`. Time comes from `ctx.Now` / `ctx.Ago` / `ctx.FromNow`, **never** `DateTimeOffset.UtcNow`.
- Take the parent from the typed argument and grandparents from `ctx.Ancestor<T>()`. Never use `ctx.Last<T>()` or `All<T>()` to wire relationships.
- **Leave derived properties unset** (`default`) so `Derive` can fill them. For `required` members, assign `null!` / `0` in the factory and let `Derive` fill them.
- Entities and parents must be **reference types**. The seed can be anything.
- A property that tests vary on the entity must be **settable**. Customizers can't assign `init` properties (CS8852). When the model must stay init-only (a `required` member with a setter can still be customized), don't change production code: use an input blueprint (below) so tests customize the input and the factory builds the row.
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
- `Blueprint.FromInput<TSeed, TInput, T>(() => new(), (input, ctx) => …)` builds a **root** from a customizable input. `T` is any reference type, an ordinary entity or an `IBiomeAggregate` (one concept saved as several rows through `GetEntities()`).
- `Blueprint.ChildFromInput<TSeed, TParent, TInput, T>(() => new(), (parent, input, ctx) => …)` builds a typed **child** from an input. Use `ctx.Ancestor<T>()` for ancestors and expect per-parent fan-out, as for `ChildOf`.
- Customizers, traits and `Derive`s (callback or sequence) act on `TInput`. `out` and the new anchor hold the output `T`. The factory initializes every `required`/`init` member of the output.
- The input is construction data: it is not recorded or saved, can't be a parent, and `All<TInput>()` is empty. A root input blueprint stays a root after `.Under(…)`; it gets no parent.
- Use inputs for rows with init-only properties tests vary, for one choice that drives several values, or for aggregates. A plain settable entity is simpler as a normal blueprint.
- `Derive` fills an input property only while it equals `default`. On an `int?`/`bool?` input an explicit `0`/`false` is kept, but an explicit `null` is still refilled if a `Derive` targets it. Output global `Default`s can also replace `null`/`0`/`false` on the output. There is no presence API.
- **Package API availability:** ordinary outputs and `ChildFromInput` require the referenced package to expose those forms. Check the selected package API before recommending an install command, and use a matching `Zeeq.Biome.EntityFrameworkCore` version when needed. Don't infer a public package version from this branch.
- Aggregate parts aren't tracked as parents, so child blueprints of an aggregate take the aggregate type as `TParent`.

## Scenario helpers

These are named, app-level chains, declared on `extension(BiomeBuilder<TSeed, TSeed> b)` so they're callable only from the seed. They return the leaf:

```csharp
public static class CodeReviewScenarioHelpers
{
    extension(BiomeBuilder<ZeeqSeed, ZeeqSeed> b)
    {
        public BiomeBuilder<ZeeqSeed, ActiveCodeReviewLock[]> AddLockedReview(
            out ActiveCodeReviewLock activeLock
        ) => b.AddCodeRepository(out _).AddPullRequest(out _).AddCodeReview(out _).AddActiveLock(out activeLock);
    }
}
```

Biome has no hidden ancestor creation. Each helper's whole chain is visible in its definition.

## Options and saving

- Configure options once, as a `static readonly BiomeOptions<TSeed>` in `TestBiome`. They freeze at the first `Biome.Start` and are shared by parallel tests.
- `SeedEntities(seed => […])` returns the seed rows in foreign-key-safe order. They are saved before everything else.
- `Default<TBase, TProp>(e => e.Prop, (e, ctx) => …)` is a fill-only default for every entity matching a class or interface. It runs after blueprint `Derive`s. The interface property needs a setter. It applies to blueprint outputs (including an input blueprint's output, not its input) and doesn't reach aggregate parts or rows added in `BeforeSave`. Like `Derive`, it can't tell an explicit default value from unset.
- `BeforeSave` / `AfterSave((save, ct) => …)`: `save.Entities` is the mutable row list, alongside `save.Seed` and `save.Session`. Rows added there skip traits, `Derive`s, and global defaults. Use these hooks sparingly, because rows a test can't see make it harder to read.
- `TimePrecision(TimeSpan)` defaults to 1 µs; `TimeSpan.Zero` turns rounding off. `Ids(prefix => …)` overrides `ctx.NewId`.
- EF Core: pass `new EfCoreStore(db)` (or `db.AsBiomeStore()`; package `Zeeq.Biome.EntityFrameworkCore`) as the store in a `TestBiome.Start(DbContext db, out var seed, TimeProvider? time = null)` overload. For database-generated keys, set navigation properties in child factories. EF also saves anything reachable through a navigation property, including transient entities.
- `SeedEntities` decides which seed objects tests can anchor on with `.Under(seed.X)`. A seed object that isn't listed can't be a parent.

## Mistakes to avoid

- **Setting a derived property in the factory.** `Derive` then never fires, because the value already looks "set".
- **Wiring relationships with `ctx.Last<T>()`.** That's zeeq-app's old `FindLatest`, and it breaks with siblings and `.Under`. Use a child blueprint and `ctx.Ancestor<T>()`.
- **Numbering with `ctx.Index + 1`.** It restarts on every Add call, which caused zeeq-app's duplicate PR #1. Use a sequence.
- **A `PerParent` sequence for a value that must be globally unique.** Two parents both produce `PR_1`.
- **Mixing one- and two-parameter lambdas in one call.** C# can't. Generated grafts expose `Action<T>` or `Action<TInput>` by default, so a one-off `ctx` customizer uses the core `Add` / `AddOne`.
- **Hand-writing the regular AddX methods.** Mark the catalog `[BiomeGraft]` and let it generate those methods. Use handwritten `AddX` extensions for a context overload or another app-specific exception.
- **Adding context overloads for every method, or overloads without `out`.** The generated set has three methods and always includes `out`. Add a named context overload only for a type whose tests repeatedly need the clock or parent (the `CodeReviews` tests' `AddCodeRepository` does). Callers still use `out _`.
- **Ancestor creation inside a child graft or extension.** Write a named scenario helper instead.
- **An input blueprint for a plain settable entity.** Customize the entity directly; use an input when the row has init-only properties tests vary, one choice drives several values, or the output is a group of rows.
- **Changing production `init` properties to `set` just so tests can customize them.** Use an input blueprint instead.
- **Changing options after the first `Start`.** It throws. Build test-specific options with a factory method instead (see `tests/Zeeq.Biome.Tests/CustomSeed/Domain.cs`).
