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

> 💡 Steal this idea; point your agent at <https://zeeq-ai.github.io/zeeq-biome/llms.txt> and ask it to build it for TypeScript, Rust, Go, etc.

## Install

```sh
dotnet add package Zeeq.Biome
dotnet add package Zeeq.Biome.EntityFrameworkCore   # optional: save with a DbContext
```

Targets .NET 10. The core package has no dependencies. The extension-block examples use C# 14; generated graft methods use regular extension methods. Works with any test framework; the examples use [TUnit](https://tunit.dev).

Ordinary-entity input blueprints and typed child inputs are available in `Zeeq.Biome` 1.1.0 and later. Use the matching version of `Zeeq.Biome.EntityFrameworkCore` when needed.

> **For coding agents:** start with the [API cheat sheet](docs/content/4.reference/1.api-cheat-sheet.md) ([web](https://zeeq-ai.github.io/zeeq-biome/reference/api-cheat-sheet)) for a quick start. The full docs are indexed for LLMs at <https://zeeq-ai.github.io/zeeq-biome/llms.txt>.

## Why Biome

Tests make expected behavior concrete, and they give an agent a way to check its changes. But the signal gets buried in setup: IDs, required fields, tenant links and timestamps repeated in every test. That fills a reader's (or an agent's) context with routine wiring that doesn't explain why the test exists. Different teammates and agents also write that setup differently, so helpers drift across the codebase.

Take a multi-tenant school system. A district report test needs one district with two schools. Here is the same setup with and without Biome:

**With Biome**

```csharp
await TestBiome.Start(_db, out _)
    .AddDistrict(out var district)
    .AddSchools(out var schools, 2)
    .BuildAsync();
_db.ChangeTracker.Clear();
```

<details>
<summary>Without Biome</summary>

```csharp
var now = DateTimeOffset.UtcNow;

var district = new District
{
    Id = $"dist_{Guid.CreateVersion7():N}",
    Name = "Riverside Unified",
    TimeZoneId = "America/Chicago",
    CreatedAtUtc = now,
    UpdatedAtUtc = now,
};
var northHigh = new School
{
    Id = $"sch_{Guid.CreateVersion7():N}",
    DistrictId = district.Id,
    Name = "North High",
    Code = "NH",
    CreatedAtUtc = now,
    UpdatedAtUtc = now,
};
var southHigh = new School
{
    Id = $"sch_{Guid.CreateVersion7():N}",
    DistrictId = district.Id,
    Name = "South High",
    Code = "SH",
    CreatedAtUtc = now,
    UpdatedAtUtc = now,
};

_db.AddRange(district, northHigh, southHigh);
await _db.SaveChangesAsync();
_db.ChangeTracker.Clear();
```

</details>

Both build the same graph. With Biome, the IDs, tenant links and timestamps live once in the blueprints, so the test reads as the scenario. The savings compound as the suite grows: less to write, less to review, less agent context spent on routine values.

`TestBiome`, `AddDistrict` and `AddSchools` are helpers you define for your app. See the [introduction](https://zeeq-ai.github.io/zeeq-biome/getting-started/introduction) for the full case.

## The generator removes the glue

Declare a blueprint once and annotate the catalog with `[BiomeGraft]`. The source generator emits typed `AddX` / `AddXs` methods for each blueprint, so you don't hand-write extension methods or pass blueprints around:

```csharp
[BiomeGraft]
public static class Blueprints
{
    public static readonly Blueprint<ZeeqSeed, CodeRepository> Repository = /* … */;
    public static readonly ChildBlueprint<ZeeqSeed, CodeRepository, PullRequestRecord> PullRequest = /* … */;
}

// Generated: AddRepository, AddRepositories, AddPullRequest, AddPullRequests
await TestBiome.Start(out _)
    .AddRepository(out var repo)
    .AddPullRequest(out var pr)   // compiles only after a repository: parent types are enforced
    .BuildAsync();
```

- A child graft compiles only when the current anchor is its parent type, so a wrong chain fails at compile time.
- `[Graft("Name", Plural = "Names")]` renames the methods and `[NoGraft]` skips a member.
- Named multi-step chains stay visible as scenario helpers.

See [Generated grafts and scenario helpers](https://zeeq-ai.github.io/zeeq-biome/guides/scenario-helpers-and-sugar).

## Examples

Eight complete domains live in [`tests/Zeeq.Biome.Tests`](tests/Zeeq.Biome.Tests), each with a seed, blueprints and tests that run in CI. A few highlights:

**Fan-out per parent** ([Learning](tests/Zeeq.Biome.Tests/Learning)): six levels in six lines.

```csharp
await TestBiome.Start(out _)
    .AddSchools(out var schools, 2)
    .AddCourses(out _, 2)          // 2 per school
    .AddSections(out var sections, 2)
    .AddEnrollments(out var enrollments, 3)
    .AddAssignments(out var assignments, 2)
    .AddSubmissions(out var submissions, 1)
    .BuildAsync();
```

**Sequences and sibling branches** ([Board](tests/Zeeq.Biome.Tests/Board)): ranks continue per column; `.Under` jumps back to an earlier parent.

```csharp
await TestBiome.Start(out _)
    .AddBoard(out var board)
    .AddColumns(out var columns, 2)
    .AddCards(out var cards, 2)    // ranks a, aa | a, aa
    .Under(columns[0])
    .AddCard(out var next)         // rank aaa: resumes this column
    .BuildAsync();
```

**Scenario helper** ([CodeReviews](tests/Zeeq.Biome.Tests/CodeReviews)): a named chain, one line in the test.

```csharp
await TestBiome.Start(out _, store).AddLockedReview(out var activeLock).BuildAsync();
```

**Call site wins** ([CodeReviews](tests/Zeeq.Biome.Tests/CodeReviews)): explicit values are kept and reserved from the sequence.

```csharp
.AddPullRequests(out var prs, _ => { }, pr => pr.Number = 1, pr => pr.Number = 3, _ => { })
// numbers: 2, 1, 3, 4
```

Also: [Membership](tests/Zeeq.Biome.Tests/Membership) (aggregates, derived expiry), [Commerce](tests/Zeeq.Biome.Tests/Commerce) (transient inputs), [CustomSeed](tests/Zeeq.Biome.Tests/CustomSeed) (global defaults, save hooks), [ProjectManagement](tests/Zeeq.Biome.Tests/ProjectManagement) and [DocsIngest](tests/Zeeq.Biome.Tests/DocsIngest).

For API shapes, the pipeline order and common errors, use the cheat sheet: [in the repo](docs/content/4.reference/1.api-cheat-sheet.md) or [on the docs site](https://zeeq-ai.github.io/zeeq-biome/reference/api-cheat-sheet). Guides and walkthroughs of every example are in the [docs](https://zeeq-ai.github.io/zeeq-biome/).

## Troubleshooting: `dotnet` exits with 137 under mise

If you install .NET with [mise](https://mise.jdx.dev), `dotnet build` or `dotnet test` can die with `Killed: 9` / exit code 137 (SIGKILL). macOS kills the shared `dotnet` host binary because its code signature is invalid. Re-sign it ad hoc:

```sh
sudo codesign --force --sign - ~/.local/share/mise/dotnet-root/dotnet
```

Re-run it if a later mise or SDK update replaces the binary.

## Versioning and contributing

- [Semantic Versioning](https://semver.org), computed from [Conventional Commits](https://www.conventionalcommits.org): `feat:` → minor, `fix:` → patch, `feat!:` / `BREAKING CHANGE:` → major.
- Releases are cut by the [Release workflow](https://github.com/zeeq-ai/zeeq-biome/actions/workflows/release.yml) (GitHub Actions → stable or rc). It publishes both packages to nuget.org and tags `vX.Y.Z`.
- Build and test: `dotnet build zeeq-biome.slnx` then `dotnet test --solution zeeq-biome.slnx`. Format: `dotnet csharpier format .`
- Agent skills for writing tests and blueprints: [`.agents/skills`](https://github.com/zeeq-ai/zeeq-biome/tree/main/.agents/skills).
- License: [MIT](https://github.com/zeeq-ai/zeeq-biome/blob/main/LICENSE).
