<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { Activity, Clock3, FilePenLine, FilePlus2, Link2, RefreshCw, RotateCcw, Server, Trash2, UserRoundCheck } from '@lucide/vue'
import MetricCard from '../components/dashboard/MetricCard.vue'
import DashboardCharts from '../components/dashboard/DashboardCharts.vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import StatusPill from '../components/common/StatusPill.vue'
import { apiRequest } from '../services/api'
import { useAsyncState } from '../composables/useAsyncState'
import { usePreferencesStore } from '../stores/preferences'
import type { ActivityPoint, DashboardSummary, DistributionPoint, ShareActivity, TopUser } from '../types/api'
import { formatDateTime, formatNumber } from '../utils/format'

const router = useRouter()
const preferences = usePreferencesStore()
const { loading, error, run } = useAsyncState()
const period = ref('7d')
const summary = ref<DashboardSummary | null>(null)
const activity = ref<ActivityPoint[]>([])
const distribution = ref<DistributionPoint[]>([])
const topUsers = ref<TopUser[]>([])
const shares = ref<ShareActivity[]>([])

const range = computed(() => {
  const to = new Date()
  const from = new Date(to)
  if (period.value === '1h') from.setHours(from.getHours() - 1)
  else if (period.value === '24h') from.setHours(from.getHours() - 24)
  else if (period.value === '30d') from.setDate(from.getDate() - 30)
  else from.setDate(from.getDate() - 7)
  return { from: from.toISOString(), to: to.toISOString() }
})

async function load() {
  await run(async () => {
    const params = new URLSearchParams({ ...range.value, mode: preferences.dataMode })
    const [summaryData, activityData, distributionData, usersData, sharesData] = await Promise.all([
      apiRequest<DashboardSummary>(`/api/v1/dashboard/summary?${params}`),
      apiRequest<ActivityPoint[]>(`/api/v1/dashboard/activity?${params}`),
      apiRequest<DistributionPoint[]>(`/api/v1/dashboard/distribution?${params}`),
      apiRequest<TopUser[]>(`/api/v1/dashboard/top-users?${params}`),
      apiRequest<ShareActivity[]>(`/api/v1/dashboard/shares?${params}`),
    ])
    summary.value = summaryData
    activity.value = activityData
    distribution.value = distributionData
    topUsers.value = usersData
    shares.value = sharesData
  })
}

function history(action?: string) {
  void router.push({ name: 'history', query: { ...(action ? { action } : {}), mode: preferences.dataMode } })
}

const collectorLabel = computed(() => ({
  healthy: 'Conectado', connected: 'Conectado', disconnected: 'Desconectado', source_unavailable: 'Journal inaccesible',
  recovering: 'Recuperando', error: 'Con errores', backend_without_agent: 'Sin agente', not_configured: 'No configurado',
}[summary.value?.collectorStatus ?? 'backend_without_agent'] ?? summary.value?.collectorStatus ?? 'Sin agente'))

watch([period, () => preferences.dataMode], load)
onMounted(load)
</script>

<template>
  <div class="dashboard">
    <section class="page-head">
      <div><p class="eyebrow">Resumen operacional</p><h2>Actividad del NAS</h2><p>Indicadores basados únicamente en eventos almacenados.</p></div>
      <div class="head-actions">
        <DataModeSwitch v-model="preferences.dataMode" />
        <select v-model="period" class="field period" aria-label="Período">
          <option value="1h">Última hora</option><option value="24h">Últimas 24 horas</option><option value="7d">Últimos 7 días</option><option value="30d">Últimos 30 días</option>
        </select>
        <button class="btn-secondary refresh" type="button" :disabled="loading" @click="load"><RefreshCw :size="16" />Actualizar</button>
      </div>
    </section>

    <div class="scope-note" :class="{ demo: preferences.dataMode === 'Demo', live: preferences.dataMode === 'Live' }">
      <StatusPill :label="preferences.dataMode === 'Demo' ? 'Entorno demo' : preferences.dataMode === 'Live' ? 'Eventos en vivo' : 'Histórico real'" :tone="preferences.dataMode === 'Demo' ? 'warning' : 'success'" />
      <span v-if="preferences.dataMode === 'Demo'">Los eventos sintéticos están aislados y nunca se suman a las métricas históricas.</span>
      <span v-else-if="preferences.dataMode === 'Live'">Solo se incluyen eventos confirmados por el recolector journald.</span>
      <span v-else>Los datos demo y en vivo están excluidos de esta consulta histórica.</span>
    </div>

    <ErrorBanner v-if="error" :message="error" @retry="load" />
    <LoadingState v-else-if="loading && !summary" />
    <template v-else-if="summary">
      <section class="metrics" aria-label="Indicadores principales">
        <MetricCard label="Eventos registrados" :value="formatNumber(summary.totalEvents)" detail="Total del origen seleccionado" :icon="Server" clickable @select="history()" />
        <MetricCard label="Últimas 24 horas" :value="formatNumber(summary.eventsLast24Hours)" detail="Actividad reciente" :icon="Clock3" clickable @select="history()" />
        <MetricCard label="Cuentas activas" :value="formatNumber(summary.activeAccounts)" detail="En el período" :icon="UserRoundCheck" tone="green" />
        <MetricCard label="Archivos creados" :value="formatNumber(summary.createdFiles)" detail="create_file · create" :icon="FilePlus2" tone="green" clickable @select="history('Creation')" />
        <MetricCard label="Modificaciones" :value="formatNumber(summary.modifiedFiles)" detail="Escritura o truncamiento" :icon="FilePenLine" clickable @select="history('Write')" />
        <MetricCard label="Eliminaciones" :value="formatNumber(summary.deletions)" detail="unlinkat confirmado" :icon="Trash2" tone="red" clickable @select="history('Deletion')" />
        <MetricCard label="Renombrados" :value="formatNumber(summary.renames)" detail="renameat confirmado" :icon="RotateCcw" tone="amber" clickable @select="history('Rename')" />
        <MetricCard label="Conexiones SMB" :value="formatNumber(summary.connections)" detail="Eventos connect" :icon="Link2" tone="slate" clickable @select="history('Connection')" />
        <MetricCard label="Último evento" :value="summary.lastEventUtc ? 'Registrado' : 'Sin datos'" :detail="formatDateTime(summary.lastEventUtc)" :icon="Activity" tone="slate" />
        <MetricCard label="Estado del recolector" :value="collectorLabel" :detail="summary.collectorStatus" :icon="Server" :tone="summary.collectorStatus === 'healthy' ? 'green' : 'amber'" />
      </section>

      <EmptyState v-if="summary.totalEvents === 0" class="panel" title="Aún no hay eventos en este origen" description="Importe un CSV histórico para comenzar. El modo demo solo muestra datos si fue habilitado explícitamente al desplegar.">
        <RouterLink class="btn-primary" to="/imports">Importar CSV</RouterLink>
      </EmptyState>
      <DashboardCharts v-else :activity="activity" :distribution="distribution" :top-users="topUsers" :shares="shares" />
    </template>
  </div>
</template>

<style scoped>
.dashboard { display: grid; gap: 15px; }
.page-head { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; }.page-head h2 { margin: 0; font-size: 24px; }.page-head p:not(.eyebrow) { margin: 6px 0 0; color: #64748b; font-size: 13px; }.eyebrow { margin: 0 0 5px; color: #1d4ed8; font-size: 10px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
.head-actions { display: flex; align-items: center; gap: 8px; }.period { width: 165px; }.refresh { min-height: 42px; }
.scope-note { display: flex; align-items: center; gap: 11px; border: 1px solid #bbf7d0; border-radius: 12px; padding: 9px 11px; color: #3f6212; background: #f7fee7; font-size: 12px; }.scope-note.demo { border-color: #fde68a; color: #92400e; background: #fffbeb; }.scope-note.live { border-color: #a5f3fc; color: #155e75; background: #ecfeff; }
.metrics { display: grid; grid-template-columns: repeat(5, minmax(150px, 1fr)); gap: 12px; }
@media (max-width: 1300px) { .metrics { grid-template-columns: repeat(3, 1fr); } }
@media (max-width: 900px) { .page-head { align-items: flex-start; flex-direction: column; }.head-actions { width: 100%; flex-wrap: wrap; }.metrics { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 560px) { .metrics { grid-template-columns: 1fr; }.head-actions > * { flex: 1; }.refresh { font-size: 0; }.refresh svg { margin: 0; }.scope-note { align-items: flex-start; flex-direction: column; } }
</style>

