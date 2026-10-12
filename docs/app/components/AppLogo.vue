<script setup lang="ts">
// Inline copy of public/logo.svg. The gradient id is unique per instance, so the
// logo can appear several times on a page without the gradients colliding.
const gradientId = `${useId()}-logo-gradient`
</script>

<template>
  <span class="inline-flex items-center gap-2">
    <svg
      viewBox="0 0 242 280"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      class="h-7 w-auto shrink-0"
      aria-hidden="true"
    >
      <defs>
        <linearGradient
          :id="gradientId"
          x1="0"
          y1="1"
          x2="1"
          y2="0"
        >
          <stop stop-color="#d3ec13" />
          <stop
            offset="1"
            stop-color="#acbe10"
          />
        </linearGradient>
      </defs>
      <path
        fill="none"
        :stroke="`url(#${gradientId})`"
        stroke-width="38"
        stroke-linejoin="miter"
        d="M182 52H110C62 52 44 68 44 117V140H119C167 140 182 124 182 78Z M44 140H126C189 140 208 158 208 211V230H112C61 230 44 198 44 165Z"
      />
    </svg>
    <span class="text-lg font-bold tracking-tight text-highlighted">
      Zeeq <span class="text-primary">Biome</span>
    </span>
  </span>
</template>
