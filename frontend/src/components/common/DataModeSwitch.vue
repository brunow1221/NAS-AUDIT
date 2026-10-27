<script setup lang="ts">
import { Activity, Database, FlaskConical } from '@lucide/vue'
import type { DataMode } from '../../types/api'

const model = defineModel<DataMode>({ required: true })
</script>

<template>
  <div class="switch" aria-label="Origen de datos">
    <button type="button" :class="{ active: model === 'Historical' }" @click="model = 'Historical'">
      <Database :size="15" />CSV histórico
    </button>
    <button type="button" :class="{ active: model === 'Demo' }" @click="model = 'Demo'">
      <FlaskConical :size="15" />Demo
    </button>
    <button type="button" :class="{ active: model === 'Live' }" @click="model = 'Live'">
      <Activity :size="15" />Journald en vivo
    </button>
  </div>
</template>

<style scoped>
.switch { display: inline-flex; gap: 3px; border: 1px solid #cbd5e1; border-radius: 11px; padding: 3px; background: #fff; }
button { display: inline-flex; align-items: center; gap: 7px; border: 0; border-radius: 8px; padding: 7px 10px; color: #64748b; background: transparent; font-size: 13px; font-weight: 700; }
button.active { color: #1d4ed8; background: #eaf2ff; box-shadow: 0 1px 2px rgba(15, 23, 42, .08); }
</style>

