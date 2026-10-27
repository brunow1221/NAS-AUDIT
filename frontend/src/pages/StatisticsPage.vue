<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { EChartsCoreOption } from 'echarts/core'
import { Activity, CalendarDays, FolderKanban, Layers3, RefreshCw } from '@lucide/vue'
import BaseChart from '../components/charts/BaseChart.vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import { apiRequest } from '../services/api'
import { usePreferencesStore } from '../stores/preferences'
import type { ActivityGroup, AuditAction, Statistics } from '../types/api'
import { actionLabels, fileName, formatDateTime, formatNumber } from '../utils/format'

const preferences = usePreferencesStore()
const period = ref('30d')
const statistics = ref<Statistics | null>(null)
const groups = ref<ActivityGroup[]>([])
const loading = ref(false)
const error = ref('')
const colors: Record<string, string> = { Creation: '#16a34a', Open: '#64748b', Write: '#2563eb', Deletion: '#dc2626', Rename: '#f59e0b', Move: '#7c3aed', Connection: '#0891b2', ObservedRead: '#0f766e', Unknown: '#94a3b8' }
const range = computed(() => { const to = new Date(); const from = new Date(to); from.setDate(from.getDate() - Number.parseInt(period.value)); return { from: from.toISOString(), to: to.toISOString() } })

async function load() {
  loading.value = true; error.value = ''
  try {
    const params = new URLSearchParams({ ...range.value, mode: preferences.dataMode })
    const [stats, activityGroups] = await Promise.all([
      apiRequest<Statistics>(`/api/v1/statistics?${params}`),
      apiRequest<ActivityGroup[]>(`/api/v1/activity-groups?${params}&limit=100`),
    ])
    statistics.value = stats; groups.value = activityGroups
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'No fue posible calcular las estadísticas.' }
  finally { loading.value = false }
}

const trendOption = computed<EChartsCoreOption>(() => {
  const rows = statistics.value?.trend ?? []
  const days = [...new Set(rows.map(item => item.dayUtc))]
  const actions = [...new Set(rows.map(item => item.action))]
  return {
    textStyle: { fontFamily: 'Segoe UI, Arial, sans-serif' }, tooltip: { trigger: 'axis' }, legend: { bottom: 0 },
    grid: { left: 42, right: 18, top: 22, bottom: 48 }, xAxis: { type: 'category', data: days.map(day => new Date(day).toLocaleDateString('es-CL', { month: 'short', day: '2-digit', timeZone: 'UTC' })) },
    yAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: '#e8eff7' } } },
    series: actions.map(action => ({ name: actionLabels[action] ?? action, type: 'line', smooth: true, symbolSize: 5, itemStyle: { color: colors[action] }, lineStyle: { color: colors[action], width: 2 }, data: days.map(day => rows.find(item => item.dayUtc === day && item.action === action)?.count ?? 0) })),
  }
})
const extensionOption = computed<EChartsCoreOption>(() => ({ textStyle: { fontFamily: 'Segoe UI, Arial, sans-serif' }, tooltip: { trigger: 'item' }, legend: { bottom: 0 }, series: [{ type: 'pie', radius: ['42%', '68%'], center: ['50%', '43%'], label: { show: false }, itemStyle: { borderWidth: 3, borderColor: '#fff' }, data: (statistics.value?.extensions ?? []).map(item => ({ name: item.name, value: item.count })) }] }))
const resultOption = computed<EChartsCoreOption>(() => ({ textStyle: { fontFamily: 'Segoe UI, Arial, sans-serif' }, tooltip: { trigger: 'axis' }, grid: { left: 90, right: 18, top: 12, bottom: 25 }, xAxis: { type: 'value', minInterval: 1 }, yAxis: { type: 'category', inverse: true, data: (statistics.value?.results ?? []).map(item => item.name) }, series: [{ type: 'bar', barWidth: 14, itemStyle: { color: '#0891b2', borderRadius: [0, 5, 5, 0] }, data: (statistics.value?.results ?? []).map(item => item.count) }] }))

watch([period, () => preferences.dataMode], load)
onMounted(load)
</script>

<template>
  <div class="statistics-page">
    <section class="page-head"><div><p class="eyebrow">Análisis operacional</p><h2>Estadísticas y agrupaciones</h2><p>Las agrupaciones son contexto técnico; no califican por sí solas una conducta.</p></div><div class="actions"><DataModeSwitch v-model="preferences.dataMode" /><select v-model="period" class="field"><option value="7">7 días</option><option value="30">30 días</option><option value="90">90 días</option><option value="365">365 días</option></select><button class="btn-secondary" @click="load"><RefreshCw :size="16" />Actualizar</button></div></section>
    <ErrorBanner v-if="error" :message="error" @retry="load" />
    <LoadingState v-if="loading && !statistics" />
    <template v-else-if="statistics">
      <section class="metrics"><article class="panel"><Activity :size="20" /><div><strong>{{ formatNumber(statistics.totalEvents) }}</strong><span>Eventos analizados</span></div></article><article class="panel"><CalendarDays :size="20" /><div><strong>{{ statistics.activeDays }}</strong><span>Días con actividad</span></div></article><article class="panel"><Layers3 :size="20" /><div><strong>{{ formatNumber(Math.round(statistics.averageEventsPerActiveDay)) }}</strong><span>Promedio por día activo</span></div></article><article class="panel"><FolderKanban :size="20" /><div><strong>{{ statistics.folders.length }}</strong><span>Carpetas destacadas</span></div></article></section>
      <EmptyState v-if="statistics.totalEvents === 0" class="panel" title="No hay actividad para analizar" description="Cambie el período o modo de datos." />
      <template v-else>
        <section class="charts"><article class="panel wide"><div class="card-head"><h3>Evolución por acción</h3><p>Conteos diarios de eventos técnicos interpretados</p></div><BaseChart :option="trendOption" /></article><article class="panel"><div class="card-head"><h3>Extensiones observadas</h3><p>Doce extensiones con mayor actividad</p></div><BaseChart :option="extensionOption" /></article><article class="panel"><div class="card-head"><h3>Resultados técnicos</h3><p>Valores informados por Samba</p></div><BaseChart :option="resultOption" /></article></section>
        <section class="lower-grid">
          <article class="panel folders"><div class="card-head"><h3>Análisis por carpeta</h3><p>Actividad agrupada por carpeta padre{{ statistics.folderAnalysisTruncated ? ' · muestra acotada a 50.000 eventos recientes' : '' }}</p></div><div class="folder-row header"><span>Carpeta</span><span>Eventos</span><span>Cuentas</span><span>Última actividad</span></div><div v-for="folder in statistics.folders" :key="`${folder.share}:${folder.folder}`" class="folder-row"><span><strong>{{ folder.folder }}</strong><small>{{ folder.share ?? 'Sin recurso' }}</small></span><span>{{ formatNumber(folder.eventCount) }}</span><span>{{ folder.accountCount }}</span><span>{{ formatDateTime(folder.lastSeenUtc) }}</span></div></article>
          <article class="panel groups"><div class="card-head"><h3>Grupos de actividad</h3><p>Eventos consecutivos de la misma cuenta, IP y ruta dentro de dos minutos</p></div><div v-for="group in groups" :key="group.groupId" class="group-card"><div><strong>{{ group.user ?? 'Cuenta no informada' }}</strong><span>{{ group.clientIp ?? 'IP no informada' }} · {{ group.share ?? 'Sin recurso' }}</span></div><span class="badge">{{ group.eventCount }} eventos</span><p>{{ fileName(group.path) }}<small>{{ group.path ?? 'Ruta no informada' }}</small></p><div class="ops"><span v-for="operation in group.operations" :key="operation">{{ operation }}</span></div><time>{{ formatDateTime(group.startedAtUtc) }} — {{ formatDateTime(group.endedAtUtc) }}</time></div></article>
        </section>
        <section class="alert-note"><strong>Preparación de alertas</strong><span>La arquitectura ya separa candidatos de alerta de los eventos. Los umbrales, horarios y notificaciones se configurarán en Fase 5 tras medir una línea base; ninguna agrupación actual se etiqueta como maliciosa.</span></section>
      </template>
    </template>
  </div>
</template>

<style scoped>
.statistics-page{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.actions{display:flex;gap:8px}.actions select{width:120px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.metrics article{display:flex;align-items:center;gap:12px;padding:15px;color:#1d4ed8}.metrics strong,.metrics span{display:block}.metrics strong{color:#0f172a;font-size:20px}.metrics span{color:#64748b;font-size:10px}.charts{display:grid;grid-template-columns:1fr 1fr;gap:14px}.charts article{padding:16px;min-width:0}.charts .wide{grid-column:1 / -1}.card-head h3{margin:0;font-size:14px}.card-head p{margin:4px 0 8px;color:#64748b;font-size:10px}.lower-grid{display:grid;grid-template-columns:1.25fr 1fr;gap:14px;align-items:start}.folders,.groups{padding:16px}.folder-row{display:grid;grid-template-columns:minmax(0,1.5fr) 70px 70px 145px;gap:8px;align-items:center;border-top:1px solid #edf2f7;padding:9px 0;font-size:10px}.folder-row.header{border:0;color:#64748b;font-size:9px;font-weight:800;text-transform:uppercase}.folder-row strong,.folder-row small{display:block}.folder-row strong{overflow:hidden;text-overflow:ellipsis;font:600 10px ui-monospace,Consolas,monospace;white-space:nowrap}.folder-row small{margin-top:2px;color:#64748b}.group-card{display:grid;grid-template-columns:1fr auto;gap:5px;border-top:1px solid #edf2f7;padding:11px 0}.group-card strong,.group-card>div>span{display:block}.group-card strong{font-size:11px}.group-card>div>span{color:#64748b;font-size:9px}.badge{height:max-content;border-radius:999px;padding:3px 7px;color:#1e40af;background:#dbeafe;font-size:9px;font-weight:800}.group-card p{grid-column:1 / -1;margin:3px 0;font-size:11px}.group-card p small{display:block;overflow:hidden;margin-top:2px;color:#64748b;text-overflow:ellipsis;white-space:nowrap}.ops{grid-column:1 / -1;display:flex;gap:4px;flex-wrap:wrap}.ops span{border-radius:5px;padding:2px 5px;color:#475569;background:#f1f5f9;font:9px ui-monospace,Consolas,monospace}.group-card time{grid-column:1 / -1;color:#94a3b8;font-size:9px}.alert-note{display:flex;gap:10px;border:1px solid #fde68a;border-radius:12px;padding:11px;color:#92400e;background:#fffbeb;font-size:11px}.alert-note strong{white-space:nowrap}@media(max-width:1100px){.metrics{grid-template-columns:1fr 1fr}.lower-grid{grid-template-columns:1fr}}@media(max-width:750px){.page-head{align-items:flex-start;flex-direction:column}.actions{width:100%;flex-wrap:wrap}.charts{grid-template-columns:1fr}.charts .wide{grid-column:auto}.folder-row{grid-template-columns:1fr auto}.folder-row>*:nth-child(3),.folder-row>*:nth-child(4){display:none}.alert-note{flex-direction:column}}@media(max-width:520px){.metrics{grid-template-columns:1fr}.actions>*{flex:1}}
</style>
