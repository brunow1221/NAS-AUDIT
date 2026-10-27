<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ChevronLeft, ChevronRight, Files, Folder, FolderClock, Home, Search, Trash2, Undo2 } from '@lucide/vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import EventTimeline from '../components/events/EventTimeline.vue'
import { ApiError, apiRequest } from '../services/api'
import { usePreferencesStore } from '../stores/preferences'
import type { FolderHistory, PagedResult, PathSummary, PathTimeline } from '../types/api'
import { actionLabels, formatDateTime, formatNumber } from '../utils/format'

const preferences = usePreferencesStore()
const paths = ref<PagedResult<PathSummary> | null>(null)
const selected = ref<PathTimeline | null>(null)
const folder = ref<FolderHistory | null>(null)
const view = ref<'folders' | 'paths'>('folders')
const period = ref('30')
const search = ref('')
const share = ref('')
const page = ref(1)
const loading = ref(false)
const detailLoading = ref(false)
const error = ref('')
const range = computed(() => { const to = new Date(); const from = new Date(to); from.setDate(from.getDate() - Number.parseInt(period.value)); return { from: from.toISOString(), to: to.toISOString() } })
const breadcrumbs = computed(() => {
  const segments = (folder.value?.summary.path ?? '/volume1').split('/').filter(Boolean)
  return segments.map((name, index) => ({ name, path: `/${segments.slice(0, index + 1).join('/')}` }))
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ mode: preferences.dataMode, page: String(page.value), pageSize: '25' })
    if (search.value.trim()) params.set('search', search.value.trim())
    if (share.value.trim()) params.set('share', share.value.trim())
    paths.value = await apiRequest<PagedResult<PathSummary>>(`/api/v1/paths?${params}`)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'No fue posible cargar las rutas.'
  } finally { loading.value = false }
}

async function openPath(path: string) {
  detailLoading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ path, mode: preferences.dataMode })
    selected.value = await apiRequest<PathTimeline>(`/api/v1/paths/history?${params}`)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'No fue posible cargar la línea de tiempo.'
  } finally { detailLoading.value = false }
}

async function openFolder(path = '/volume1') {
  detailLoading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ path, ...range.value, mode: preferences.dataMode })
    folder.value = await apiRequest<FolderHistory>(`/api/v1/folders/history?${params}`)
  } catch (reason) {
    if (reason instanceof ApiError && reason.status === 404) {
      folder.value = null
      return
    }
    error.value = reason instanceof Error ? reason.message : 'No fue posible cargar la carpeta observada.'
  } finally { detailLoading.value = false }
}

function changeView(next: 'folders' | 'paths') {
  view.value = next
  error.value = ''
  if (next === 'folders') void openFolder(folder.value?.summary.path ?? '/volume1')
  else void load()
}

function apply() { page.value = 1; void load() }
watch([period, () => preferences.dataMode], () => {
  selected.value = null; page.value = 1
  if (view.value === 'folders') void openFolder('/volume1'); else void load()
})
watch(page, load)
onMounted(() => openFolder())
</script>

<template>
  <div class="explorer">
    <section class="page-head">
      <div><p class="eyebrow">Trazabilidad documental</p><h2>Explorador histórico</h2><p>Consulta rutas observadas; el sistema nunca abre ni modifica los archivos originales.</p></div>
      <div class="head-actions"><DataModeSwitch v-model="preferences.dataMode" /><select v-model="period" class="field"><option value="7">7 días</option><option value="30">30 días</option><option value="90">90 días</option><option value="365">365 días</option></select></div>
    </section>

    <section class="view-switch"><button :class="{ active: view === 'folders' }" @click="changeView('folders')"><Folder :size="16" />Carpetas observadas</button><button :class="{ active: view === 'paths' }" @click="changeView('paths')"><Files :size="16" />Buscar rutas</button></section>

    <section v-if="view === 'paths'" class="panel filters">
      <div class="search"><label class="label" for="path-search">Ruta o archivo</label><div><Search :size="17" /><input id="path-search" v-model="search" class="field" placeholder="Ej. proyecto o documento.pdf" @keyup.enter="apply" /></div></div>
      <div><label class="label" for="path-share">Recurso compartido</label><input id="path-share" v-model="share" class="field" placeholder="Todos" @keyup.enter="apply" /></div>
      <button class="btn-primary" type="button" @click="apply"><Search :size="16" />Buscar</button>
    </section>

    <ErrorBanner v-if="error" :message="error" @retry="view === 'folders' ? openFolder(folder?.summary.path ?? '/volume1') : load()" />
    <div v-if="view === 'paths'" class="workspace">
      <section class="panel paths-panel">
        <div class="panel-head"><div><strong>Rutas observadas</strong><span>{{ formatNumber(paths?.totalItems ?? 0) }} resultados</span></div></div>
        <LoadingState v-if="loading && !paths" />
        <EmptyState v-else-if="paths?.items.length === 0" title="No encontramos rutas" description="Pruebe otro término, recurso o modo de datos." />
        <button v-for="item in paths?.items" :key="item.path" class="path-row" :class="{ active: selected?.summary.path === item.path }" type="button" @click="openPath(item.path)">
          <FolderClock :size="18" />
          <span class="path"><strong>{{ item.path }}</strong><small>{{ item.share ?? 'Recurso no informado' }} · {{ item.accountCount }} cuenta(s)</small></span>
          <span class="count">{{ formatNumber(item.eventCount) }}</span>
          <time>{{ formatDateTime(item.lastSeenUtc) }}</time>
        </button>
        <div v-if="paths && paths.totalPages > 1" class="pager"><button class="btn-secondary" :disabled="page <= 1" @click="page--"><ChevronLeft :size="16" /></button><span>Página {{ page }} de {{ paths.totalPages }}</span><button class="btn-secondary" :disabled="page >= paths.totalPages" @click="page++"><ChevronRight :size="16" /></button></div>
      </section>

      <section class="panel detail-panel">
        <LoadingState v-if="detailLoading" />
        <EmptyState v-else-if="!selected" title="Seleccione una ruta" description="Verá su primera y última aparición, cuentas relacionadas y cronología técnica." />
        <template v-else>
          <div class="detail-head"><div><p class="eyebrow">Línea de tiempo</p><h3>{{ selected.summary.path }}</h3><span>{{ selected.summary.share ?? 'Recurso no informado' }}</span></div><span v-if="selected.possiblyDeleted" class="deleted"><Trash2 :size="14" />Posible eliminación</span><span v-else-if="selected.renamedOrMovedAway" class="moved"><Undo2 :size="14" />Ruta anterior</span></div>
          <div class="facts"><div><span>Eventos</span><strong>{{ formatNumber(selected.summary.eventCount) }}</strong></div><div><span>Cuentas</span><strong>{{ selected.summary.accountCount }}</strong></div><div><span>Primera aparición</span><strong>{{ formatDateTime(selected.summary.firstSeenUtc) }}</strong></div><div><span>Última aparición</span><strong>{{ formatDateTime(selected.summary.lastSeenUtc) }}</strong></div></div>
          <p v-if="selected.events.length < selected.summary.eventCount" class="limit-note">Se muestran los 250 eventos más recientes de esta ruta.</p>
          <EventTimeline :events="selected.events" />
        </template>
      </section>
    </div>
    <div v-else class="workspace folder-workspace">
      <section class="panel paths-panel">
        <nav class="breadcrumbs"><button type="button" title="Raíz observada" @click="openFolder('/volume1')"><Home :size="14" /></button><button v-for="crumb in breadcrumbs" :key="crumb.path" type="button" @click="openFolder(crumb.path)">{{ crumb.name }}</button></nav>
        <div class="panel-head"><div><strong>Subcarpetas observadas</strong><span>{{ folder?.subfolders.length ?? 0 }} en este nivel · no es un inventario del NAS</span></div></div>
        <LoadingState v-if="detailLoading && !folder" />
        <EmptyState v-else-if="folder?.subfolders.length === 0" title="Sin subcarpetas observadas" description="No hay rutas hijas en la evidencia del período seleccionado." />
        <button v-for="item in folder?.subfolders" :key="item.path" class="path-row folder-row" type="button" @click="openFolder(item.path)"><Folder :size="18" /><span class="path"><strong>{{ item.name }}</strong><small>{{ item.path }}</small></span><span class="count">{{ formatNumber(item.eventCount) }}</span><time>{{ formatDateTime(item.lastSeenUtc) }}</time></button>
      </section>
      <section class="panel detail-panel">
        <LoadingState v-if="detailLoading" />
        <EmptyState v-else-if="!folder" title="Sin evidencia de carpetas" description="Cambie el período o el origen de datos." />
        <template v-else>
          <div class="detail-head"><div><p class="eyebrow">Carpeta observada</p><h3>{{ folder.summary.path }}</h3><span>{{ folder.summary.share ?? 'Recurso deducido desde las rutas registradas' }}</span></div></div>
          <div class="facts"><div><span>Eventos</span><strong>{{ formatNumber(folder.summary.eventCount) }}</strong></div><div><span>Cuentas</span><strong>{{ folder.summary.accountCount }}</strong></div><div><span>Primera aparición</span><strong>{{ formatDateTime(folder.summary.firstSeenUtc) }}</strong></div><div><span>Última aparición</span><strong>{{ formatDateTime(folder.summary.lastSeenUtc) }}</strong></div></div>
          <p v-if="folder.pathSampleTruncated" class="limit-note">La detección de subcarpetas usa una muestra acotada a 50.000 rutas recientes; los totales de eventos sí corresponden al período completo.</p>
          <div class="folder-breakdown"><article><strong>Acciones</strong><div v-for="item in folder.actions" :key="item.name"><span>{{ actionLabels[item.name] ?? item.name }}</span><b>{{ formatNumber(item.count) }}</b></div></article><article><strong>Cuentas involucradas</strong><div v-for="item in folder.accounts" :key="item.name"><span>{{ item.name }}</span><b>{{ formatNumber(item.count) }}</b></div></article></div>
          <h4>Eventos recientes de la carpeta y sus descendientes</h4>
          <EventTimeline :events="folder.recentEvents" />
        </template>
      </section>
    </div>
  </div>
</template>

<style scoped>
.explorer{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.head-actions{display:flex;gap:8px}.head-actions select{width:110px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.view-switch{display:flex;width:max-content;border:1px solid #bfdbfe;border-radius:11px;padding:3px;background:#fff}.view-switch button{display:flex;align-items:center;gap:6px;border:0;border-radius:8px;padding:8px 12px;color:#64748b;background:transparent;font-size:11px;font-weight:700}.view-switch button.active{color:#1d4ed8;background:#dbeafe}.filters{display:grid;grid-template-columns:2fr 1fr auto;gap:10px;align-items:end;padding:14px}.search>div{position:relative}.search svg{position:absolute;left:12px;top:13px;color:#94a3b8}.search input{padding-left:38px}.workspace{display:grid;grid-template-columns:minmax(380px,.9fr) minmax(0,1.4fr);gap:14px;align-items:start}.paths-panel,.detail-panel{overflow:hidden}.panel-head{padding:14px 16px;border-bottom:1px solid #dbeafe}.panel-head strong,.panel-head span{display:block}.panel-head span{margin-top:2px;color:#64748b;font-size:11px}.breadcrumbs{display:flex;gap:3px;overflow:auto;border-bottom:1px solid #dbeafe;padding:8px}.breadcrumbs button{display:flex;align-items:center;border:0;border-radius:6px;padding:5px 7px;color:#1d4ed8;background:#eff6ff;font-size:10px}.path-row{display:grid;width:100%;grid-template-columns:auto minmax(0,1fr) auto;gap:10px;align-items:center;border:0;border-bottom:1px solid #edf2f7;padding:12px 15px;background:#fff;color:#334155;text-align:left}.path-row:hover,.path-row.active{background:#eff6ff}.path-row.active{box-shadow:inset 3px 0 #2563eb}.path-row>svg{color:#3b82f6}.folder-row>svg{color:#f59e0b}.path{min-width:0}.path strong,.path small{display:block}.path strong{overflow:hidden;text-overflow:ellipsis;font:600 11px ui-monospace,SFMono-Regular,Consolas,monospace;white-space:nowrap}.path small{margin-top:4px;color:#64748b;font-size:10px}.count{border-radius:999px;padding:3px 7px;background:#dbeafe;color:#1e40af;font-size:10px;font-weight:800}.path-row time{grid-column:2 / -1;color:#94a3b8;font-size:9px}.pager{display:flex;align-items:center;justify-content:center;gap:10px;padding:12px}.pager button{min-height:34px;padding:5px 9px}.pager span{font-size:11px;color:#64748b}.detail-panel{padding:17px;max-height:calc(100vh - 210px);overflow:auto}.detail-head{display:flex;justify-content:space-between;gap:15px}.detail-head h3{margin:0;word-break:break-all;font-size:15px}.detail-head>div>span{color:#64748b;font-size:11px}.deleted{display:flex;align-items:center;gap:5px;height:max-content;border-radius:999px;padding:5px 8px;color:#b91c1c;background:#fef2f2;font-size:10px;font-weight:800;white-space:nowrap}.facts{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:15px 0}.facts div{border:1px solid #dbeafe;border-radius:10px;padding:10px;background:#f8fbff}.facts span,.facts strong{display:block}.facts span{color:#64748b;font-size:9px;text-transform:uppercase}.facts strong{margin-top:4px;font-size:11px}.limit-note{color:#92400e;background:#fffbeb;border-radius:8px;padding:8px;font-size:10px}.folder-breakdown{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:15px}.folder-breakdown article{border:1px solid #dbeafe;border-radius:10px;padding:11px}.folder-breakdown article>strong{display:block;margin-bottom:7px;color:#1e3a8a;font-size:11px}.folder-breakdown article>div{display:flex;justify-content:space-between;gap:8px;border-top:1px solid #edf2f7;padding:5px 0;font-size:9px}.detail-panel h4{margin:15px 0 8px;font-size:12px}@media(max-width:1100px){.workspace{grid-template-columns:1fr}.detail-panel{max-height:none}}@media(max-width:700px){.page-head{align-items:flex-start;flex-direction:column}.head-actions{width:100%;flex-wrap:wrap}.filters{grid-template-columns:1fr}.workspace{display:block}.paths-panel{margin-bottom:14px}.facts,.folder-breakdown{grid-template-columns:1fr 1fr}}
.moved{display:flex;align-items:center;gap:5px;height:max-content;border-radius:999px;padding:5px 8px;color:#92400e;background:#fffbeb;font-size:10px;font-weight:800;white-space:nowrap}
</style>
