<script setup lang="ts">
const { seo } = useAppConfig()
const base = useRuntimeConfig().app.baseURL
const route = useRoute()
const site = useSiteConfig()
const canonical = computed(() => `${site.url}${base.replace(/\/$/, '')}${route.path}`)

const { data: navigation } = await useAsyncData('navigation', () => queryCollectionNavigation('docs'))
const { data: files } = useLazyAsyncData('search', () => queryCollectionSearchSections('docs'), {
  server: false
})

useHead({
  meta: [
    { name: 'viewport', content: 'width=device-width, initial-scale=1' }
  ],
  link: [
    { rel: 'icon', href: `${base}favicon.svg`, type: 'image/svg+xml' },
    { rel: 'icon', href: `${base}favicon.ico`, sizes: '48x48' },
    { rel: 'apple-touch-icon', href: `${base}apple-touch-icon.png` },
    { rel: 'canonical', href: canonical }
  ],
  htmlAttrs: {
    lang: 'en'
  }
})

useSeoMeta({
  titleTemplate: `%s - ${seo?.siteName}`,
  ogSiteName: seo?.siteName,
  ogUrl: canonical,
  twitterCard: 'summary_large_image'
})

provide('navigation', navigation)
</script>

<template>
  <UApp>
    <NuxtLoadingIndicator />

    <AppHeader />

    <UMain>
      <NuxtLayout>
        <NuxtPage />
      </NuxtLayout>
    </UMain>

    <AppFooter />

    <ClientOnly>
      <LazyUContentSearch
        :files="files"
        :navigation="navigation"
      />
    </ClientOnly>
  </UApp>
</template>
