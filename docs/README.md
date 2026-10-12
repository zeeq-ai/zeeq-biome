# Zeeq Biome documentation

The documentation site for [Zeeq Biome](https://github.com/zeeq-ai/zeeq-biome), published at <https://zeeq-ai.github.io/zeeq-biome/>. Built with [Nuxt UI](https://ui.nuxt.com) and [Nuxt Content](https://content.nuxt.com), starting from the [docs template](https://github.com/nuxt-ui-templates/docs).

## Run locally

Node and pnpm are pinned (`.nvmrc`, `packageManager` in `package.json`, and `.config/mise.toml` at the repository root). With [mise](https://mise.jdx.dev): `mise install`.

```sh
cd docs
pnpm install
pnpm dev        # http://localhost:3000/ (served from / in development)
```

## Build the static site

The site deploys to a sub-path (`/zeeq-biome/`), so a production build bakes that base URL in. Override it with `NUXT_APP_BASE_URL`.

```sh
pnpm generate                          # base URL /zeeq-biome/ -> .output/public
NUXT_APP_BASE_URL=/ pnpm generate      # same site, served from the root
pnpm preview                           # serve .output/public
pnpm lint && pnpm typecheck
```

| Variable | Default | Purpose |
|---|---|---|
| `NUXT_APP_BASE_URL` | `/zeeq-biome/` (`/` in `pnpm dev`) | Base path the site is served from |
| `NUXT_PUBLIC_SITE_URL` | `https://zeeq-ai.github.io` | Origin used for canonical links and OG tags |

## Writing content

Pages live in `content/`. Numeric prefixes (`01.`, `02.`) set the order in the sidebar; use two digits when a folder has more than nine pages. Conventions:

- Code blocks carry a label and highlight only the lines that carry the point: ` ```csharp [File.cs] {3-5} `.
- Use `::code-group` for alternatives. For a "With Biome" / "Without Biome" comparison, the long tab is a collapsed `::code-collapse{label="Without Biome" name="setup"}` placed inside the group.
- Callouts: `::note`, `::tip`, `::warning`, `::caution` (there is no `::important`).
- Prefer snippets copied from `tests/`, and never document an API that does not exist in `src/`.

## Deploy

Releases deploy the site automatically (`.github/workflows/release.yml` -> `docs-deploy.yml`, after the NuGet publish). To redeploy without a release, run the **Docs** workflow (`docs.yml`) from `main`. Pull requests only build the site (`ci.yml`). One-time setting: **Settings -> Pages -> Source: GitHub Actions**.

## Logo

`public/logo.svg` is the mark: a leaf-shaped **B** with a Zeeq yellow-to-green gradient (`#d3ec13` to `#acbe10`), a wider lower leaf and a uniform 38-unit stroke. `public/favicon.svg` has the same geometry with its own gradient id. `logo.png` (256px), `favicon.ico` (16/32/48) and `apple-touch-icon.png` (180px) are rendered from them:

```sh
pnpm run render:logo     # scripts/render-logo.mjs, uses sharp
```

The header (`app/components/AppLogo.vue`) and the OG image (`app/components/OgImage/Docs.takumi.vue`) hold inline copies of the path. Update them together with the SVGs. Keep each gradient `id` unique, because `AppLogo` can render more than once on a page (it derives the id with `useId()`).
