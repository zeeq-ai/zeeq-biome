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
          class="shrink-0"
          width="42"
          height="48"
          viewBox="0 0 242 280"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <linearGradient
              id="og-logo-gradient"
              x1="0"
              y1="1"
              x2="1"
              y2="0"
            >
              <stop stop-color="#79bd3e" />
              <stop
                offset="1"
                stop-color="#009b48"
              />
            </linearGradient>
          </defs>
          <path
            fill="none"
            stroke="url(#og-logo-gradient)"
            stroke-width="38"
            stroke-linejoin="miter"
            d="M182 52H110C62 52 44 68 44 117V140H119C167 140 182 124 182 78Z M44 140H126C189 140 208 158 208 211V230H112C61 230 44 198 44 165Z"
          />
        </svg>
        <div class="h-px flex-1 bg-border" />
        <span class="text-xl text-dimmed">Zeeq Biome · zeeq-ai.github.io/zeeq-biome</span>
      </div>
    </div>
  </div>
</template>
