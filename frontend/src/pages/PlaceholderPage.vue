<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Construction, FileClock, ShieldCheck } from '@lucide/vue'

const route = useRoute()
const title = computed(() => String(route.meta.title ?? 'Módulo'))
const phase = computed(() => Number(route.meta.phase ?? 2))
</script>

<template>
  <div class="placeholder panel">
    <div class="icon"><Construction :size="28" /></div>
    <p>Hoja de ruta · Fase {{ phase }}</p>
    <h2>{{ title }}</h2>
    <span>La arquitectura contempla este módulo, pero no se simula funcionalidad fuera de la fase autorizada.</span>
    <div class="facts"><div><FileClock :size="18" /><span><strong>Disponible ahora</strong>Dashboard e historial importado</span></div><div><ShieldCheck :size="18" /><span><strong>Alcance protegido</strong>Sin cambios en Samba ni archivos</span></div></div>
    <RouterLink class="btn-primary" to="/dashboard">Volver al dashboard</RouterLink>
  </div>
</template>

<style scoped>
.placeholder { display: grid; min-height: calc(100vh - 116px); place-items: center; align-content: center; padding: 38px; text-align: center; }.icon { display: grid; place-items: center; width: 62px; height: 62px; border-radius: 18px; color: #1d4ed8; background: #eaf2ff; }.placeholder > p { margin: 17px 0 6px; color: #1d4ed8; font-size: 10px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }.placeholder h2 { margin: 0; font-size: 28px; }.placeholder > span { max-width: 510px; margin-top: 8px; color: #64748b; line-height: 1.6; }.facts { display: grid; width: min(500px, 100%); grid-template-columns: 1fr 1fr; gap: 10px; margin: 27px 0; }.facts div { display: flex; align-items: center; gap: 10px; border: 1px solid #dbeafe; border-radius: 11px; padding: 12px; color: #1d4ed8; background: #f7fbff; text-align: left; }.facts span,.facts strong { display: block; }.facts span { color: #64748b; font-size: 11px; }.facts strong { margin-bottom: 2px; color: #334155; font-size: 12px; }@media (max-width: 550px) { .facts { grid-template-columns: 1fr; } }
</style>

