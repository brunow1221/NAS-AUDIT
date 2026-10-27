<script setup lang="ts">
import { Circle } from '@lucide/vue'
withDefaults(defineProps<{ label: string; tone?: 'success' | 'warning' | 'danger' | 'neutral' }>(), { tone: 'neutral' })
</script>

<template>
  <span class="status" :class="`status--${tone}`"><Circle :size="8" fill="currentColor" />{{ label }}</span>
</template>

<style scoped>
.status { display: inline-flex; align-items: center; gap: 7px; width: fit-content; white-space: nowrap; border: 1px solid #cbd5e1; border-radius: 999px; padding: 6px 10px; color: #475569; background: #f8fafc; font-size: 12px; font-weight: 700; }
.status--success { color: #15803d; border-color: #bbf7d0; background: #f0fdf4; }
.status--warning { color: #a16207; border-color: #fde68a; background: #fffbeb; }
.status--danger { color: #b91c1c; border-color: #fecaca; background: #fef2f2; }
</style>

