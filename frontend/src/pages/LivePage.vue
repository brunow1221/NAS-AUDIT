<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { Activity, CirclePause, CirclePlay, RefreshCw, Search, Wifi, WifiOff } from '@lucide/vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import StatusPill from '../components/common/StatusPill.vue'
import { apiRequest } from '../services/api'
import type { AuditEvent, CollectorStatus } from '../types/api'
import { actionLabels, fileName, formatDate, formatDateTime, formatTime } from '../utils/format'
import { LiveEventBuffer } from '../utils/liveEventBuffer'

const status = ref<CollectorStatus | null>(null)
const statusError = ref('')
const connection = ref<'connecting' | 'connected' | 'reconnecting' | 'closed'>('connecting')
const events = ref<AuditEvent[]>([])
const pending = ref(0)
const paused = ref(false)
const user = ref('')
const operation = ref('')
const share = ref('')
const search = ref('')
const buffer = new LiveEventBuffer(500)
let source: EventSource | null = null
let statusTimer: number | undefined

const visible = computed(() => events.value.filter(event =>
  (!user.value || event.smbUser?.toLowerCase().includes(user.value.toLowerCase())) &&
  (!operation.value || event.operation === operation.value) &&
  (!share.value || event.shareName?.toLowerCase().includes(share.value.toLowerCase())) &&
  (!search.value || [event.filePath, event.smbUser, event.clientIp].some(value => value?.toLowerCase().includes(search.value.toLowerCase())))))
const statusTone = computed<'success' | 'warning' | 'danger' | 'neutral'>(() => {
  if (status.value?.status === 'connected' || status.value?.status === 'active_no_events') return 'success'
  if (status.value?.status === 'recovering') return 'warning'
  if (['source_unavailable', 'error', 'database_unavailable', 'backend_unavailable'].includes(status.value?.status ?? '')) return 'danger'
  return 'neutral'
})

async function loadStatus() {
  try {
    status.value = await apiRequest<CollectorStatus>('/api/v1/collector/status')
    statusError.value = ''
  } catch (reason) {
    statusError.value = reason instanceof Error ? reason.message : 'No fue posible consultar el recolector.'
    let databaseUnavailable = false
    try {
      databaseUnavailable = (await fetch('/health/ready', { credentials: 'include' })).status === 503
    } catch { /* La API completa no está disponible. */ }
    status.value = {
      status: databaseUnavailable ? 'database_unavailable' : 'backend_unavailable',
      label: databaseUnavailable ? 'Base de datos no disponible' : 'Backend no disponible',
      collectorId: null, lastHeartbeatUtc: null, lastEventUtc: null, confirmedCursor: null, queueDepth: 0,
      message: databaseUnavailable ? 'PostgreSQL no respondió a la comprobación de disponibilidad.' : 'No fue posible contactar la API.',
    }
  }
}
function connect() {
  source?.close()
  connection.value = 'connecting'
  source = new EventSource('/api/v1/events/stream', { withCredentials: true })
  source.onopen = () => { connection.value = 'connected' }
  source.onerror = () => { connection.value = 'reconnecting' }
  source.addEventListener('audit-event', message => {
    try {
      const auditEvent = JSON.parse((message as MessageEvent<string>).data) as AuditEvent
      if (buffer.push(auditEvent)) {
        events.value = [...buffer.visible()]
        pending.value = buffer.pendingCount()
      }
    } catch { /* La reconexión entregará nuevamente eventos persistidos. */ }
  })
}
function togglePause() {
  if (paused.value) buffer.resume()
  else buffer.pause()
  paused.value = buffer.isPaused()
  pending.value = buffer.pendingCount()
  events.value = [...buffer.visible()]
}
function reconnect() { connect(); void loadStatus() }

onMounted(() => {
  void loadStatus()
  connect()
  statusTimer = window.setInterval(loadStatus, 20_000)
})
onBeforeUnmount(() => {
  source?.close()
  connection.value = 'closed'
  window.clearInterval(statusTimer)
})
</script>

<template>
  <div class="live">
    <section class="page-head"><div><p class="eyebrow">Recolección journald</p><h2>Actividad en tiempo real</h2><p>La conexión de esta pantalla y la salud del recolector se verifican por separado.</p></div><button class="btn-secondary" type="button" @click="reconnect"><RefreshCw :size="17" />Reconectar</button></section>

    <section class="status-grid">
      <article class="panel status-card"><div class="status-icon" :class="statusTone"><Activity :size="23" /></div><div><span>Estado del recolector</span><strong>{{ status?.label ?? 'Consultando…' }}</strong><small>{{ status?.message ?? 'Salud informada por el agente del NAS' }}</small></div><StatusPill :label="status?.status ?? 'unknown'" :tone="statusTone" /></article>
      <article class="panel status-card"><div class="status-icon" :class="connection === 'connected' ? 'success' : 'warning'"><Wifi v-if="connection === 'connected'" :size="23" /><WifiOff v-else :size="23" /></div><div><span>Canal de visualización</span><strong>{{ connection === 'connected' ? 'SSE conectado' : connection === 'reconnecting' ? 'Reconectando…' : 'Conectando…' }}</strong><small>Con reconexión automática y deduplicación</small></div></article>
      <article class="panel status-card"><div class="status-icon neutral"><Activity :size="23" /></div><div><span>Última sincronización</span><strong>{{ formatDateTime(status?.lastHeartbeatUtc ?? null) }}</strong><small>{{ status?.queueDepth ?? 0 }} evento(s) en cola del agente</small></div></article>
    </section>
    <ErrorBanner v-if="statusError" :message="statusError" @retry="loadStatus" />

    <section class="panel controls">
      <div class="search-box"><label class="label" for="live-search">Buscar</label><div><Search :size="16" /><input id="live-search" v-model="search" class="field" placeholder="Archivo, cuenta o IP" /></div></div>
      <div><label class="label" for="live-user">Cuenta</label><input id="live-user" v-model.trim="user" class="field" placeholder="Todas" /></div>
      <div><label class="label" for="live-operation">Operación</label><select id="live-operation" v-model="operation" class="field"><option value="">Todas</option><option value="create_file">create_file</option><option value="pwrite">pwrite</option><option value="ftruncate">ftruncate</option><option value="renameat">renameat</option><option value="unlinkat">unlinkat</option><option value="connect">connect</option></select></div>
      <div><label class="label" for="live-share">Recurso</label><input id="live-share" v-model.trim="share" class="field" placeholder="Todos" /></div>
      <button class="btn-primary pause" type="button" @click="togglePause"><CirclePlay v-if="paused" :size="18" /><CirclePause v-else :size="18" />{{ paused ? `Reanudar${pending ? ` (${pending})` : ''}` : 'Pausar vista' }}</button>
    </section>

    <section class="panel table-card">
      <div class="table-head"><div><strong>{{ visible.length }} eventos visibles</strong><span v-if="paused">La vista está pausada; la recolección y el almacenamiento continúan.</span><span v-else>Más recientes primero · máximo 500 en pantalla</span></div></div>
      <EmptyState v-if="visible.length === 0" :title="status?.status === 'active_no_events' ? 'Monitoreo activo — sin eventos nuevos' : 'Aún no hay eventos en vivo'" description="Valide primero con una operación en PRUEBA_AUDITORIA_SMB. No pruebe sobre carpetas productivas." />
      <div v-else class="table-scroll"><table><thead><tr><th>Fecha</th><th>Hora</th><th>Cuenta SMB</th><th>IP</th><th>Acción</th><th>Operación</th><th>Archivo</th><th>Ruta</th><th>Recurso</th><th>Resultado</th></tr></thead><tbody><tr v-for="event in visible" :key="event.id"><td>{{ formatDate(event.eventTimestampUtc) }}</td><td><strong>{{ formatTime(event.eventTimestampUtc) }}</strong></td><td>{{ event.smbUser ?? '—' }}</td><td class="mono">{{ event.clientIp ?? '—' }}</td><td>{{ actionLabels[event.interpretedAction] ?? event.interpretedAction }}</td><td class="mono">{{ event.operation }}</td><td>{{ fileName(event.filePath) }}</td><td class="path" :title="event.filePath ?? ''">{{ event.filePath ?? '—' }}</td><td>{{ event.shareName ?? '—' }}</td><td :class="event.operationResult.toLowerCase() === 'ok' ? 'ok' : 'failed'">{{ event.operationResult }}</td></tr></tbody></table></div>
    </section>
  </div>
</template>

<style scoped>
.live{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.status-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.status-card{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:12px;padding:14px}.status-card>div:nth-child(2) span,.status-card>div:nth-child(2) strong,.status-card>div:nth-child(2) small{display:block}.status-card span{color:#64748b;font-size:9px;text-transform:uppercase}.status-card strong{margin-top:2px;font-size:13px}.status-card small{margin-top:3px;color:#64748b;font-size:10px}.status-icon{display:grid;place-items:center;width:43px;height:43px;border-radius:12px}.status-icon.success{color:#15803d;background:#ecfdf3}.status-icon.warning{color:#b45309;background:#fffbeb}.status-icon.danger{color:#b91c1c;background:#fef2f2}.status-icon.neutral{color:#1d4ed8;background:#eff6ff}.controls{display:grid;grid-template-columns:1.5fr repeat(3,1fr) auto;align-items:end;gap:10px;padding:14px}.search-box>div{position:relative}.search-box svg{position:absolute;left:12px;top:13px;color:#94a3b8}.search-box input{padding-left:37px}.pause{white-space:nowrap}.table-card{overflow:hidden}.table-head{padding:14px 16px;border-bottom:1px solid #dbeafe}.table-head strong,.table-head span{display:block}.table-head strong{font-size:13px}.table-head span{margin-top:3px;color:#64748b;font-size:10px}.table-scroll{max-height:calc(100vh - 385px);overflow:auto}table{width:100%;border-collapse:collapse;font-size:11px}th{position:sticky;top:0;z-index:1;padding:10px 11px;color:#3b5f83;background:#eaf4fc;text-align:left;font-size:9px;text-transform:uppercase;white-space:nowrap}td{max-width:270px;border-top:1px solid #edf2f7;padding:9px 11px;color:#334155;white-space:nowrap}.path{overflow:hidden;text-overflow:ellipsis}.mono{font-family:ui-monospace,SFMono-Regular,Consolas,monospace}.ok{color:#15803d;font-weight:800}.failed{color:#b91c1c;font-weight:800}
@media(max-width:1100px){.status-grid{grid-template-columns:1fr}.controls{grid-template-columns:repeat(2,1fr)}.pause{grid-column:2}}@media(max-width:650px){.page-head{align-items:flex-start;flex-direction:column}.controls{grid-template-columns:1fr}.pause{grid-column:auto}.status-card{grid-template-columns:auto 1fr}.status-card>:last-child{grid-column:2}}
</style>
