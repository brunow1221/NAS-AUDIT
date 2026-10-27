<script setup lang="ts">
import { DatabaseZap } from '@lucide/vue'
defineProps<{ title: string; description: string }>()
</script>

<template>
  <div class="empty">
    <div class="empty__icon"><DatabaseZap :size="25" /></div>
    <h3>{{ title }}</h3>
    <p>{{ description }}</p>
    <slot />
  </div>
</template>

<style scoped>
.empty { display: grid; place-items: center; min-height: 240px; padding: 36px 20px; text-align: center; }
.empty__icon { display: grid; place-items: center; width: 52px; height: 52px; border-radius: 16px; color: #1d4ed8; background: #eaf2ff; }
h3 { margin: 14px 0 4px; font-size: 17px; }
p { max-width: 450px; margin: 0 0 18px; color: #64748b; font-size: 14px; line-height: 1.6; }
</style>

