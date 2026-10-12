// https://nuxt.com/docs/api/configuration/nuxt-config
import { readdirSync } from 'node:fs'
import { join } from 'node:path'

// GitHub Pages serves this repository under /zeeq-biome/. Override with NUXT_APP_BASE_URL
// (for example `/`) to serve the generated site from a different path.
const siteUrl = process.env.NUXT_PUBLIC_SITE_URL || 'https://zeeq-ai.github.io'

// Markdown twins of every docs page (`/raw/<path>.md`) power "Copy page" and "View as Markdown".
// A static host has no server routes, so prerender each one explicitly.
function rawMarkdownRoutes(dir = join(import.meta.dirname, 'content'), prefix = ''): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const name = entry.name.replace(/^\d+\./, '')
    if (entry.isDirectory()) {
      return rawMarkdownRoutes(join(dir, entry.name), `${prefix}/${name}`)
    }
    if (!entry.name.endsWith('.md') || (prefix === '' && name === 'index.md')) {
      return []
    }
    const slug = name.replace(/\.md$/, '')
    return [`/raw${slug === 'index' ? prefix : `${prefix}/${slug}`}.md`]
  })
}

export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/image',
    '@nuxt/ui',
    '@nuxt/content',
    'nuxt-og-image',
    'nuxt-llms'
  ],

  // `nuxt dev` serves from / unless NUXT_APP_BASE_URL says otherwise.
  $development: {
    app: {
      baseURL: process.env.NUXT_APP_BASE_URL || '/'
    }
  },

  devtools: {
    enabled: true
  },

  app: {
    baseURL: process.env.NUXT_APP_BASE_URL || '/zeeq-biome/'
  },

  css: ['~/assets/css/main.css'],

  site: {
    url: siteUrl,
    name: 'Zeeq Biome'
  },

  content: {
    build: {
      markdown: {
        toc: {
          searchDepth: 1
        },
        highlight: {
          theme: {
            default: 'github-light',
            dark: 'github-dark'
          },
          langs: ['csharp', 'bash', 'json', 'yaml', 'xml', 'ts', 'vue', 'md']
        }
      }
    },
    experimental: {
      sqliteConnector: 'native'
    }
  },

  experimental: {
    asyncContext: true
  },

  compatibilityDate: '2026-06-30',

  eslint: {
    config: {
      stylistic: {
        commaDangle: 'never',
        braceStyle: '1tbs'
      }
    }
  },

  llms: {
    domain: `${siteUrl}/zeeq-biome`,
    title: 'Zeeq Biome',
    description: 'Terse, readable test setup for .NET. A biome is your app\'s entity graph in miniature: just the rows one test needs, wired together correctly.',
    full: {
      title: 'Zeeq Biome - Full Documentation',
      description: 'The full documentation for Zeeq Biome, a .NET 10 / C# 14 test-setup library.'
    },
    sections: [
      {
        title: 'Getting Started',
        contentCollection: 'docs',
        contentFilters: [
          { field: 'path', operator: 'LIKE', value: '/getting-started%' }
        ]
      },
      {
        title: 'Guides',
        contentCollection: 'docs',
        contentFilters: [
          { field: 'path', operator: 'LIKE', value: '/guides%' }
        ]
      },
      {
        title: 'Examples',
        contentCollection: 'docs',
        contentFilters: [
          { field: 'path', operator: 'LIKE', value: '/examples%' }
        ]
      },
      {
        title: 'Reference',
        contentCollection: 'docs',
        contentFilters: [
          { field: 'path', operator: 'LIKE', value: '/reference%' },
          { field: 'path', operator: 'LIKE', value: '/agents%' }
        ]
      }
    ]
  },

  ogImage: {
    zeroRuntime: true
  },

  prerender: {
    routes: [
      '/',
      ...rawMarkdownRoutes()
    ],
    crawlLinks: true
  }
})
