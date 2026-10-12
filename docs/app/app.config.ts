export default defineAppConfig({
  ui: {
    colors: {
      primary: 'green',
      neutral: 'zinc'
    },
    footer: {
      slots: {
        root: 'border-t border-default',
        left: 'text-sm text-muted'
      }
    }
  },
  seo: {
    siteName: 'Zeeq Biome'
  },
  header: {
    title: '',
    to: '/',
    logo: {
      alt: '',
      light: '',
      dark: ''
    },
    search: true,
    colorMode: true,
    links: [{
      'icon': 'i-simple-icons-github',
      'to': 'https://github.com/zeeq-ai/zeeq-biome',
      'target': '_blank',
      'aria-label': 'GitHub'
    }, {
      'icon': 'i-simple-icons-nuget',
      'to': 'https://www.nuget.org/packages/Zeeq.Biome',
      'target': '_blank',
      'aria-label': 'NuGet'
    }]
  },
  footer: {
    credits: `Zeeq Biome • MIT License • © ${new Date().getFullYear()} Zeeq`,
    colorMode: false,
    links: [{
      'icon': 'i-simple-icons-github',
      'to': 'https://github.com/zeeq-ai/zeeq-biome',
      'target': '_blank',
      'aria-label': 'Zeeq Biome on GitHub'
    }, {
      'icon': 'i-simple-icons-nuget',
      'to': 'https://www.nuget.org/packages/Zeeq.Biome',
      'target': '_blank',
      'aria-label': 'Zeeq.Biome on NuGet'
    }]
  },
  toc: {
    title: 'On this page',
    bottom: {
      title: 'Links',
      edit: 'https://github.com/zeeq-ai/zeeq-biome/edit/main/docs/content',
      links: [{
        icon: 'i-lucide-star',
        label: 'Star on GitHub',
        to: 'https://github.com/zeeq-ai/zeeq-biome',
        target: '_blank'
      }, {
        icon: 'i-lucide-package',
        label: 'Zeeq.Biome on NuGet',
        to: 'https://www.nuget.org/packages/Zeeq.Biome',
        target: '_blank'
      }]
    }
  }
})
