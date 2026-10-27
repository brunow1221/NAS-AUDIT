<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ChevronDown, ChevronLeft, ChevronRight, Download, Eye, FilterX, Search, X } from '@lucide/vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import StatusPill from '../components/common/StatusPill.vue'
import { useAsyncState } from '../composables/useAsyncState'
import { apiRequest, downloadFile } from '../services/api'
import { usePreferencesStore } from '../stores/preferences'
import type { AuditAction, AuditEvent, PagedResult } from '../types/api'
import { actionLabels, fileName, formatDate, formatDateTime, formatTime } from '../utils/format'

const route = useRoute()
const preferences = usePreferencesStore()
const { loading, error, run } = useAsyncState()
const result = ref<PagedResult<AuditEvent>>({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 })
const selected = ref<AuditEvent | null>(null)
const detailLoading = ref(false)
const exporting = ref(false)
const advanced = ref(false)
const page = ref(1)
const filters = reactive({
  search: typeof route.query.search === 'string' ? route.query.search : '',
  action: (typeof route.query.action === 'string' ? route.query.action : '') as AuditAction | '',
  user: '', clientIp: '', operation: '', share: '', fileName: '', path: '', extension: '',
  result: '', source: '', confidence: '', from: '', to: '', sortBy: 'timestamp', descending: 'true',
})
let debounce: number | undefined

const params = computed(() => {
  const value = new URLSearchParams({ page: String(page.value), pageSize: '25', mode: preferences.dataMode })
  Object.entries(filters).forEach(([key, entry]) => {
    if (!entry || key === 'from' || key === 'to') return
    value.set(key, entry)
  })
  if (filters.from) value.set('from', new Date(`${filters.from}T00:00:00`).toISOString())
  if (filters.to) value.set('to', new Date(`${filters.to}T23:59:59.999`).toISOString())
  return value
})

async function load() {
  await run(async () => { result.value = await apiRequest<PagedResult<AuditEvent>>(`/api/v1/events?${params.value}`) })
}

function reset() {
  Object.assign(filters, { search: '', action: '', user: '', clientIp: '', operation: '', share: '', fileName: '', path: '', extension: '', result: '', source: '', confidence: '', from: '', to: '', sortBy: 'timestamp', descending: 'true' })
  page.value = 1
}
function scheduleLoad() { window.clearTimeout(debounce); debounce = window.setTimeout(() => { page.value = 1; void load() }, 300) }
function actionTone(value: AuditAction): 'success' | 'warning' | 'danger' | 'neutral' {
  if (value === 'Creation') return 'success'
  if (value === 'Deletion') return 'danger'
  if (value === 'Rename' || value === 'Move') return 'warning'
  return 'neutral'
}
async function showDetail(event: AuditEvent) {
  selected.value = event
  detailLoading.value = true
  try { selected.value = await apiRequest<AuditEvent>(`/api/v1/events/${event.id}`) }
  finally { detailLoading.value = false }
}
async function exportCsv() {
  exporting.value = true
  try {
    const exportParams = new URLSearchParams(params.value)
    exportParams.delete('page')
    exportParams.delete('pageSize')
    await downloadFile(`/api/v1/events/export?${exportParams}`)
  } finally { exporting.value = false }
}

watch(() => preferences.dataMode, () => { page.value = 1; void load() })
watch(page, load)
watch(() => [filters.action, filters.user, filters.clientIp, filters.operation, filters.share, filters.fileName, filters.path, filters.extension, filters.result, filters.source, filters.confidence, filters.from, filters.to, filters.sortBy, filters.descending], () => { page.value = 1; void load() })
watch(() => filters.search, scheduleLoad)
watch(() => route.query.action, value => { filters.action = (typeof value === 'string' ? value : '') as AuditAction | '' })
onMounted(() => {
  if (route.query.mode === 'Demo' || route.query.mode === 'Live') preferences.dataMode = route.query.mode
  void load()
})
</script>

<template>
  <div class="history">
    <section class="page-head">
      <div><p class="eyebrow">Evidencia almacenada</p><h2>Historial de auditoría</h2><p>Consulta paginada, filtrada y exportable de toda la evidencia disponible.</p></div>
      <div class="head-actions"><DataModeSwitch v-model="preferences.dataMode" /><button class="btn-secondary" type="button" :disabled="exporting || loading" @click="exportCsv"><Download :size="17" />{{ exporting ? 'Exportando…' : 'Exportar CSV' }}</button></div>
    </section>

    <section class="panel filter-panel">
      <div class="filters-primary">
        <div class="search-box"><label class="label" for="search">Búsqueda global</label><div><Search :size="17" /><input id="search" v-model="filters.search" class="field" placeholder="Ruta, archivo, cuenta, IP o mensaje…" /></div></div>
        <div><label class="label" for="action">Acción interpretada</label><select id="action" v-model="filters.action" class="field"><option value="">Todas</option><option v-for="(label, value) in actionLabels" :key="value" :value="value">{{ label }}</option></select></div>
        <div><label class="label" for="from">Desde</label><input id="from" v-model="filters.from" class="field" type="date" /></div>
        <div><label class="label" for="to">Hasta</label><input id="to" v-model="filters.to" class="field" type="date" /></div>
        <button class="btn-ghost advanced-button" type="button" @click="advanced = !advanced"><ChevronDown :size="17" :class="{ rotated: advanced }" />Filtros</button>
        <button class="btn-ghost clear" type="button" title="Limpiar filtros" @click="reset"><FilterX :size="18" />Limpiar</button>
      </div>
      <div v-if="advanced" class="filters-advanced">
        <div><label class="label" for="user">Cuenta SMB</label><input id="user" v-model.trim="filters.user" class="field" placeholder="Todas" /></div>
        <div><label class="label" for="ip">Dirección IP</label><input id="ip" v-model.trim="filters.clientIp" class="field" placeholder="Todas" /></div>
        <div><label class="label" for="operation">Operación técnica</label><input id="operation" v-model.trim="filters.operation" class="field" placeholder="Ej. pwrite" /></div>
        <div><label class="label" for="share">Recurso</label><input id="share" v-model.trim="filters.share" class="field" placeholder="Todos" /></div>
        <div><label class="label" for="filename">Nombre de archivo</label><input id="filename" v-model.trim="filters.fileName" class="field" placeholder="Ej. informe.pdf" /></div>
        <div><label class="label" for="path">Ruta contiene</label><input id="path" v-model.trim="filters.path" class="field" placeholder="Ej. /Proyectos/" /></div>
        <div><label class="label" for="extension">Extensión</label><input id="extension" v-model.trim="filters.extension" class="field" placeholder="Ej. pdf" /></div>
        <div><label class="label" for="result">Resultado</label><input id="result" v-model.trim="filters.result" class="field" placeholder="Ej. ok" /></div>
        <div><label class="label" for="source">Origen</label><input id="source" v-model.trim="filters.source" class="field" placeholder="Ej. CSV:…" /></div>
        <div><label class="label" for="confidence">Interpretación</label><select id="confidence" v-model="filters.confidence" class="field"><option value="">Todas</option><option value="Confirmed">Confirmada</option><option value="Medium">Media</option><option value="Low">Baja</option><option value="Unknown">Desconocida</option></select></div>
        <div><label class="label" for="sort">Ordenar por</label><select id="sort" v-model="filters.sortBy" class="field"><option value="timestamp">Fecha</option><option value="user">Cuenta</option><option value="action">Acción</option><option value="operation">Operación</option><option value="result">Resultado</option></select></div>
        <div><label class="label" for="direction">Dirección</label><select id="direction" v-model="filters.descending" class="field"><option value="true">Descendente</option><option value="false">Ascendente</option></select></div>
      </div>
    </section>

    <ErrorBanner v-if="error" :message="error" @retry="load" />
    <section v-else class="panel table-card">
      <div class="table-head"><div><strong>{{ result.totalItems.toLocaleString('es-CL') }} eventos</strong><span>{{ preferences.dataMode === 'Demo' ? 'Datos sintéticos aislados' : 'Histórico productivo' }}</span></div></div>
      <LoadingState v-if="loading" />
      <EmptyState v-else-if="result.items.length === 0" title="No hay eventos para estos filtros" description="Cambie los criterios o importe un archivo CSV histórico." />
      <div v-else class="table-scroll"><table><thead><tr><th>Fecha y hora</th><th>Cuenta SMB</th><th>IP</th><th>Acción</th><th>Operación</th><th>Archivo</th><th>Recurso</th><th>Resultado</th><th><span class="sr-only">Detalle</span></th></tr></thead><tbody><tr v-for="event in result.items" :key="event.id"><td><strong>{{ formatTime(event.eventTimestampUtc) }}</strong><span>{{ formatDate(event.eventTimestampUtc) }}</span></td><td>{{ event.smbUser ?? '—' }}</td><td class="mono">{{ event.clientIp ?? '—' }}</td><td><StatusPill :label="actionLabels[event.interpretedAction] ?? event.interpretedAction" :tone="actionTone(event.interpretedAction)" /></td><td class="mono">{{ event.operation }}</td><td class="file"><strong :title="event.filePath ?? ''">{{ fileName(event.filePath) }}</strong><span :title="event.filePath ?? ''">{{ event.filePath ?? 'Sin ruta informada' }}</span></td><td>{{ event.shareName ?? '—' }}</td><td><span :class="event.operationResult.toLowerCase() === 'ok' ? 'ok' : 'failed'">{{ event.operationResult }}</span></td><td><button class="detail" type="button" title="Ver detalle" @click="showDetail(event)"><Eye :size="18" /></button></td></tr></tbody></table></div>
      <footer v-if="result.items.length" class="pagination"><span>Página {{ result.page }} de {{ Math.max(result.totalPages, 1) }}</span><div><button class="btn-secondary" type="button" :disabled="result.page <= 1" @click="page--"><ChevronLeft :size="17" />Anterior</button><button class="btn-secondary" type="button" :disabled="result.page >= result.totalPages" @click="page++">Siguiente<ChevronRight :size="17" /></button></div></footer>
    </section>

    <div v-if="selected" class="drawer-backdrop" @click="selected = null" />
    <aside v-if="selected" class="drawer" aria-label="Detalle del evento">
      <header><div><p class="eyebrow">Evento {{ selected.id }}</p><h3>{{ actionLabels[selected.interpretedAction] }}</h3></div><button type="button" aria-label="Cerrar" @click="selected = null"><X :size="21" /></button></header>
      <p v-if="detailLoading" class="detail-loading">Actualizando detalle…</p>
      <dl>
        <div><dt>Fecha local</dt><dd>{{ formatDateTime(selected.eventTimestampUtc) }}</dd></div><div><dt>Fecha UTC</dt><dd class="mono">{{ selected.eventTimestampUtc }}</dd></div>
        <div><dt>Cuenta / IP</dt><dd>{{ selected.smbUser ?? '—' }} · {{ selected.clientIp ?? '—' }}</dd></div><div><dt>Operación técnica</dt><dd class="mono">{{ selected.operation }}</dd></div>
        <div><dt>Acción / certeza</dt><dd>{{ actionLabels[selected.interpretedAction] }} · {{ selected.interpretationConfidence }}</dd></div><div><dt>Resultado</dt><dd>{{ selected.operationResult }}</dd></div>
        <div><dt>Recurso / objeto</dt><dd>{{ selected.shareName ?? '—' }} · {{ selected.objectType ?? 'tipo no informado' }}</dd></div><div><dt>Ruta observada</dt><dd class="path">{{ selected.filePath ?? '—' }}</dd></div>
        <div v-if="selected.sourcePath"><dt>Ruta original</dt><dd class="path">{{ selected.sourcePath }}</dd></div><div v-if="selected.destinationPath"><dt>Ruta destino</dt><dd class="path">{{ selected.destinationPath }}</dd></div>
        <div><dt>Observaciones</dt><dd><span v-if="selected.isAuxiliaryStream">Flujo auxiliar de metadatos. </span><span v-if="selected.isRecyclePath">Ruta de papelera observada. </span><span v-if="!selected.isAuxiliaryStream && !selected.isRecyclePath">Sin observaciones técnicas adicionales.</span></dd></div>
        <div><dt>Origen</dt><dd>{{ selected.sourceName }} ({{ selected.sourceKind }}) · {{ selected.parserVersion }}</dd></div><div v-if="selected.journalCursor"><dt>Cursor journald</dt><dd class="path">{{ selected.journalCursor }}</dd></div>
        <div><dt>Mensaje original</dt><dd class="raw">{{ selected.rawMessage }}</dd></div>
      </dl>
    </aside>
  </div>
</template>

<style scoped>
.history{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.head-actions{display:flex;align-items:center;gap:9px}.filter-panel{padding:14px}.filters-primary{display:grid;grid-template-columns:minmax(280px,2fr) minmax(150px,1fr) 145px 145px auto auto;align-items:end;gap:10px}.search-box>div{position:relative}.search-box svg{position:absolute;left:12px;top:13px;color:#94a3b8}.search-box input{padding-left:38px}.advanced-button,.clear{min-height:42px;white-space:nowrap}.advanced-button svg{transition:transform .15s}.advanced-button svg.rotated{transform:rotate(180deg)}.filters-advanced{display:grid;grid-template-columns:repeat(6,minmax(130px,1fr));gap:10px;margin-top:13px;border-top:1px solid #dbeafe;padding-top:13px}.table-card{overflow:hidden}.table-head{display:flex;padding:15px 17px;border-bottom:1px solid #dbeafe}.table-head strong,.table-head span{display:block}.table-head strong{font-size:14px}.table-head span{margin-top:2px;color:#64748b;font-size:11px}.table-scroll{overflow-x:auto}table{width:100%;border-collapse:collapse;font-size:12px}th{padding:11px 13px;color:#3b5f83;background:#eaf4fc;text-align:left;font-size:10px;letter-spacing:.05em;text-transform:uppercase;white-space:nowrap}td{max-width:280px;border-top:1px solid #e8f0f7;padding:10px 13px;color:#334155;vertical-align:middle}tbody tr:nth-child(even){background:#fbfdff}tbody tr:hover{background:#f1f7fd}td strong,td span{display:block}td>span{color:#64748b}.file strong,.file span{max-width:280px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.file span{margin-top:3px;font-size:10px}.mono{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:11px}.ok{width:fit-content;color:#15803d;font-weight:800}.failed{width:fit-content;color:#b91c1c;font-weight:800}.detail{display:grid;place-items:center;border:0;border-radius:8px;padding:7px;color:#1d4ed8;background:#eaf2ff}.pagination{display:flex;align-items:center;justify-content:space-between;border-top:1px solid #dbeafe;padding:12px 15px;color:#64748b;font-size:12px}.pagination div{display:flex;gap:7px}.pagination button{min-height:36px;padding:7px 11px;font-size:12px}.drawer-backdrop{position:fixed;inset:0;z-index:60;background:rgba(15,23,42,.25);backdrop-filter:blur(2px)}.drawer{position:fixed;inset:0 0 0 auto;z-index:65;width:min(560px,94vw);overflow-y:auto;border-left:1px solid #bfdbfe;padding:22px;background:#fff;box-shadow:-15px 0 45px rgba(15,23,42,.14)}.drawer header{display:flex;align-items:flex-start;justify-content:space-between;border-bottom:1px solid #dbeafe;padding-bottom:16px}.drawer h3{margin:0;font-size:22px}.drawer header button{border:0;border-radius:8px;padding:7px;color:#64748b;background:#f1f5f9}.drawer dl{display:grid;gap:0;margin:12px 0 0}.drawer dl div{border-bottom:1px solid #edf2f7;padding:13px 0}.drawer dt{margin-bottom:5px;color:#64748b;font-size:10px;font-weight:800;letter-spacing:.06em;text-transform:uppercase}.drawer dd{margin:0;font-size:13px}.path,.raw{overflow-wrap:anywhere;font-family:ui-monospace,SFMono-Regular,Consolas,monospace}.raw{border-radius:9px;padding:10px;color:#cbd5e1;background:#0f172a;font-size:11px!important;line-height:1.55}.detail-loading{margin:10px 0 0;color:#64748b;font-size:11px}.sr-only{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0)}
@media(max-width:1350px){.filters-primary{grid-template-columns:2fr repeat(3,1fr)}.advanced-button,.clear{grid-row:2}.filters-advanced{grid-template-columns:repeat(4,1fr)}}@media(max-width:850px){.page-head{align-items:flex-start;flex-direction:column}.head-actions{width:100%;flex-wrap:wrap}.filters-primary,.filters-advanced{grid-template-columns:repeat(2,1fr)}.search-box{grid-column:1/-1}.advanced-button,.clear{grid-row:auto}.pagination{align-items:flex-start;flex-direction:column;gap:10px}}@media(max-width:520px){.filters-primary,.filters-advanced{grid-template-columns:1fr}.search-box{grid-column:auto}.head-actions>*{flex:1}}
</style>
