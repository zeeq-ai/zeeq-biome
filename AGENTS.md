# Zeeq Biome Testing Utility Library for .NET

- Uses C# 14, .NET 10; modern language features
  - Extension blocks, source generators, tuples, pattern matching, collection expressions, etc.
  - Uses `TUnit` for testing (not `XUnit`).
- Intended to help teams write unit tests that are more terse, clear, and readable by both humans and agents by isolating repeated boilerplate code in test scenario setup
- Semantic versioning is configured in CI; follow it
- Publishes packages to Nuget and docs to GitHub pages

## Key Principles

- Value concise, terse writing: core source, examples, docs, writing
- Always think about how to improve ergonomics, reduce repetition, reduce cognitive load for all parties when reading code
- Prioritize readability and maintainability over cleverness
- Leverage static analysis when possible to signal early failures for consumers of this toolkit:
  - Use types and structural organization of code (e.g. generics, interfaces, composition, C#'s type inference)
  - Use Roslyn analyzers where it can help users reduce mistakes
- Always think about future travelers who need to quickly understand the decisions
- Comments are a secret weapon: free long-term memory to capture key decisions, reasoning, rationale

## How to Work

- Keep the `./docs` updated when making changes
  - An undocumented change to the external API is a risk and a hidden feature
  - Use clear examples, highlight source lines, and use inline comments to explain
  - Consistency is important in writing style, voice, and formatting
  - Don't immediately arrive at the conclusion; guide the reasoning and peel the layers of "why"
  - Don't just assume "better", also explain the gap that was closed
  - Examples are useless if reader has to guess
  - `./docs/content/4.reference/1.api-cheat-sheet.md` is good starting point (and keep it updated with tips for future travelers)
- Read `./docs` for current state and philosophy
- Use Paseo skills when operating in Paseo environment
  - Use Paseo built-in browser if available
- If in Paseo and possible to work with peers:
  - Check active work and ownership
  - Coordinate when necessary
  - If your work can be parallelized and delegated, do so and coordinate it to release
