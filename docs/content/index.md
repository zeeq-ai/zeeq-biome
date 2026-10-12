---
seo:
  title: Zeeq Biome
  description: Terse, readable tests for you and your coding agents. Reusable setup reduces the context needed to author and understand each test.
---

::u-page-hero{class="dark:bg-gradient-to-b from-neutral-900 to-neutral-950"}
---
orientation: horizontal
---
#top
:hero-background

#title
Readable tests. [Less context for your agents]{.brand-gradient-text}.

#description
Tests give coding agents a way to check that their changes produce the intended behavior. **Zeeq Biome** keeps routine setup in reusable blueprints, making tests terse and readable. With fewer setup details in each test, you and your agents need less context to author and understand it. It targets .NET 10; the extension-block examples use C# 14.

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
Keep the behavior easy to see.

#features
  :::u-page-feature
  ---
  icon: i-lucide-focus
  ---
  #title
  Only the property under test

  #description
  Repeating defaults makes the important value harder to spot. Blueprints handle the routine properties, while the test chooses the values it depends on.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-network
  ---
  #title
  Typed parents and children

  #description
  Related rows should agree about their parent. Child blueprints receive that parent directly, so the chain shows how each entity belongs to the one above it.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-list-ordered
  ---
  #title
  Sequences, not index + 1

  #description
  A counter based on an array index restarts at the next Add. Sequences keep counting within the scope you choose and skip values the test supplies.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-clock
  ---
  #title
  One clock per test

  #description
  Small timestamp differences can break an otherwise correct test. Use a frozen clock or share a `FakeTimeProvider`, with 1 µs precision to match Postgres.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-database
  ---
  #title
  Pluggable persistence

  #description
  The same blueprints can serve unit and database tests. Build entities in memory, save once through EF Core, or supply your own `IBiomeStore`.
  :::

  :::u-page-feature
  ---
  icon: i-lucide-bot
  ---
  #title
  Agent skills included

  #description
  Agents need guidance on the same choices you make in setup. Two included skills explain how to write test chains and maintain the blueprints behind them.
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
  title: Start with one test.
  description: Move routine setup into a blueprint so the test is easier for you and your agents to write, read, and check. The quick start walks through each choice.
  class: dark:bg-neutral-950
  ---

  :stars-bg
  :::
::
