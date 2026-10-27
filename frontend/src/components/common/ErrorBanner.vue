<script setup lang="ts">
import { AlertTriangle, RotateCcw } from '@lucide/vue'
defineProps<{ message: string }>()
defineEmits<{ retry: [] }>()
</script>

<template>
  <div class="error" role="alert">
    <AlertTriangle :size="20" />
    <div><strong>No pudimos cargar la información</strong><p>{{ message }}</p></div>
    <button class="btn-secondary" type="button" @click="$emit('retry')"><RotateCcw :size="16" />Reintentar</button>
  </div>
</template>

<style scoped>
.error { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 12px; border: 1px solid #fecaca; border-radius: 12px; padding: 12px 14px; color: #991b1b; background: #fef2f2; }
.error p { margin: 2px 0 0; color: #b91c1c; font-size: 13px; }
@media (max-width: 620px) { .error { grid-template-columns: auto 1fr; } .error button { grid-column: 2; width: fit-content; } }
</style>

