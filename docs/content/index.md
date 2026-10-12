---
seo:
  title: Zeeq Biome
  description: Terse, readable test setup for .NET. A biome is your app's entity graph in miniature, with just the rows one test needs, wired together correctly.
---

::u-page-hero{class="dark:bg-gradient-to-b from-neutral-900 to-neutral-950"}
---
orientation: horizontal
---
#top
:hero-background

#title
Test setup that says [only what the test is about]{.text-primary}.

#description
**Zeeq Biome** builds the slice of your entity graph one test needs: valid defaults, typed parents and children, sequences for numbers and ordering, a controlled clock, and pluggable persistence. A `.NET 10` / `C# 14` library. Defaults never overwrite a value the test set.

#links
  :::u-button
  ---
  to: /getting-started/introduction
  size: xl
  trailing-icon: i-lucide-arrow-right
  ---
  Get started
  :::

  :::u-button
  ---
  icon: i-simple-icons-github
  color: neutral
  variant: outline
  size: xl
  to: https://github.com/zeeq-ai/zeeq-biome
  target: _blank
  ---
  GitHub
  :::

#default
  :::prose-pre
  ---
  code: |
    await TestBiome.Start(_db, out var seed)
        .AddCodeRepository(out var repo)
        .AddPullRequest(out var pr, p => p.State = PullRequestState.Closed)
        .BuildAsync();
  filename: CodeReviewTests.cs
  highlights:
    - 3
  ---

  ```csharp [CodeReviewTests.cs] {3}
  await TestBiome.Start(_db, out var seed)
      .AddCodeRepository(out var repo)
      .AddPullRequest(out var pr, p => p.State = PullRequestState.Closed)
      .BuildAsync();
  ```
  :::
::

::u-page-section{class="dark:bg-neutral-950"}
#title
Say what you are testing. Nothing else.

#features
  :::u-page-feature
  ---
  icon: i-lucide-focus
  ---
  #title
  Only the property under test

  #description
  Blueprints supply valid, uninteresting defaults. A call site sets just the property the test is about, and nothing overwrites it.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-network
  ---
  #title
  Typed parents and children

  #description
  A child blueprint takes its parent as a typed argument. Every Add re-anchors to its output, so a chain reads top to bottom.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-list-ordered
  ---
  #title
  Sequences, not index + 1

  #description
  Declare a numbering or ranking sequence once. Scope it per session, per parent, or per ancestor, and explicit values are skipped.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-clock
  ---
  #title
  One clock per test

  #description
  Frozen by default, or share a `FakeTimeProvider` with the code under test. Values round to 1 µs, matching Postgres `timestamptz`.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-database
  ---
  #title
  Pluggable persistence

  #description
  Save through EF Core with one `SaveChangesAsync`, skip the database for unit tests, or implement a one-method `IBiomeStore`.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-bot
  ---
  #title
  Agent skills included

  #description
  Two skills teach coding agents to write test chains and author blueprints the way this library intends.
  :::
::

::u-page-section{class="dark:bg-gradient-to-b from-neutral-950 to-neutral-900"}
  :::u-page-c-t-a
  ---
  links:
    - label: Quick start
      to: '/getting-started/quick-start'
      trailingIcon: i-lucide-arrow-right
    - label: Browse examples
      to: '/examples'
      variant: subtle
      icon: i-lucide-library
  title: Make your next test setup shorter than the assertion.
  description: Install the NuGet package, write one blueprint, and see the difference in your first test.
  class: dark:bg-neutral-950
  ---

  :stars-bg
  :::
::
