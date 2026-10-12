<script setup lang="ts">
defineOptions({
  inheritAttrs: false
})

defineProps<{
  headline?: string
  title?: string
  description?: string
}>()
</script>

<template>
  <div
    class="w-full h-full flex bg-neutral-950 relative overflow-hidden"
    data-theme="dark"
  >
    <div class="absolute top-0 left-0 w-1.5 h-full bg-primary-400" />

    <div class="flex flex-col justify-between flex-1 px-20 py-16">
      <div />

      <div class="flex flex-col gap-5">
        <span
          v-if="headline"
          class="text-2xl font-medium text-primary-400"
        >
          {{ headline }}
        </span>

        <h1
          v-if="title"
          class="text-6xl font-bold text-highlighted"
        >
          {{ title }}
        </h1>

        <p
          v-if="description"
          class="text-3xl/11 text-muted"
          :style="{ lineClamp: 2, textOverflow: 'ellipsis' }"
        >
          {{ description }}
        </p>
      </div>

      <div class="flex items-center gap-4">
        <svg
          class="size-10"
          viewBox="0 0 64 64"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            d="M17 9V55M17 9H31A11 11 0 0 1 31 31H17M17 31H31A11 12 0 0 1 31 55H17"
            stroke="#c026d3"
            stroke-width="6.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
          <path
            d="M43.8 20V43"
            stroke="#e879f9"
            stroke-width="2.4"
            stroke-linecap="round"
          />
          <g fill="#e879f9"><path d="M43.8 20Q54.7 17.5 57.2 6.6Q46.2 9 43.8 20Z" /><path d="M43.8 43Q54.7 40.5 57.2 29.6Q46.2 32 43.8 43Z" /><path d="M43.8 32.5Q42.6 25.1 35.3 24Q36.4 31.4 43.8 32.5Z" /></g>
        </svg>
        <div class="h-px flex-1 bg-border" />
        <span class="text-xl text-dimmed">Zeeq Biome · zeeq-ai.github.io/zeeq-biome</span>
      </div>
    </div>
  </div>
</template>
