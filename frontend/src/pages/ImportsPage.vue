<script setup lang="ts">
import { computed, onMounted, ref, shallowRef } from 'vue'
import { AlertTriangle, CheckCircle2, FileSpreadsheet, History, Info, UploadCloud, X } from '@lucide/vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import StatusPill from '../components/common/StatusPill.vue'
import { useAsyncState } from '../composables/useAsyncState'
import { apiRequest } from '../services/api'
import type { CsvPreview, ImportJob } from '../types/api'
import { formatDateTime, formatNumber } from '../utils/format'

const { loading, error, run } = useAsyncState()
const jobs = ref<ImportJob[]>([])
const files = shallowRef<File[]>([])
const dragActive = ref(false)
const results = ref<ImportJob[]>([])
const preview = ref<CsvPreview | null>(null)
const progress = ref({ current: 0, total: 0 })
const previewColumns = computed(() => preview.value?.rows[0] ? Object.keys(preview.value.rows[0]).slice(0, 8) : [])

async function load() { await run(async () => { jobs.value = await apiRequest<ImportJob[]>('/api/v1/imports') }) }
function pick(event: Event) { const input = event.target as HTMLInputElement; void selectFiles(Array.from(input.files ?? [])); input.value = '' }
function drop(event: DragEvent) { dragActive.value = false; void selectFiles(Array.from(event.dataTransfer?.files ?? [])) }
async function selectFiles(selected: File[]) {
  files.value = selected.filter(file => file.name.toLowerCase().endsWith('.csv'))
  preview.value = null
  results.value = []
  const firstFile = files.value[0]
  if (!firstFile) return
  await run(async () => {
    const form = new FormData(); form.append('file', firstFile, firstFile.name)
    preview.value = await apiRequest<CsvPreview>('/api/v1/imports/preview', { method: 'POST', body: form })
  })
}
function removeFile(index: number) {
  files.value = files.value.filter((_, fileIndex) => fileIndex !== index)
  preview.value = null
  if (files.value[0]) void selectFiles([...files.value])
}
async function upload() {
  if (!files.value.length) return
  results.value = []
  progress.value = { current: 0, total: files.value.length }
  await run(async () => {
    for (const file of files.value) {
      const form = new FormData(); form.append('file', file)
      results.value.push(await apiRequest<ImportJob>('/api/v1/imports', { method: 'POST', body: form }))
      progress.value.current++
    }
    files.value = []
    preview.value = null
    jobs.value = await apiRequest<ImportJob[]>('/api/v1/imports')
  })
}
function tone(status: ImportJob['status']): 'success' | 'warning' | 'danger' | 'neutral' {
  if (status === 'Completed') return 'success'
  if (status === 'Failed') return 'danger'
  if (status === 'CompletedWithWarnings' || status === 'Duplicate') return 'warning'
  return 'neutral'
}
onMounted(load)
</script>

<template>
  <div class="imports">
    <section class="page-head"><div><p class="eyebrow">Incorporación controlada</p><h2>Importar registros históricos</h2><p>Valide, previsualice e incorpore exportaciones CSV de UGOS Pro.</p></div></section>
    <div class="notice"><Info :size="19" /><p><strong>Importador de Fase 2.</strong> Detecta formato, codificación y delimitador; aplica gramática por operación, fechas de Santiago, deduplicación entre exportaciones y trazabilidad por fila.</p></div>
    <ErrorBanner v-if="error" :message="error" @retry="load" />
    <section class="import-grid">
      <article class="panel upload-card">
        <div class="card-head"><span><UploadCloud :size="20" /></span><div><h3>Nueva importación</h3><p>Uno o varios CSV · máximo 20 MB cada uno</p></div></div>
        <label class="dropzone" :class="{ active: dragActive }" @dragenter.prevent="dragActive = true" @dragleave.prevent="dragActive = false" @dragover.prevent @drop.prevent="drop">
          <input type="file" accept=".csv,text/csv" multiple @change="pick" /><FileSpreadsheet :size="33" /><strong>{{ files.length ? `${files.length} archivo(s) seleccionado(s)` : 'Seleccione o arrastre CSV' }}</strong><span>Registros de sucesos o transmisión</span>
        </label>
        <div v-if="files.length" class="file-list"><div v-for="(file, index) in files" :key="`${file.name}-${file.size}`"><FileSpreadsheet :size="16" /><span><strong>{{ file.name }}</strong><small>{{ (file.size / 1024).toFixed(1) }} KB</small></span><button type="button" aria-label="Quitar archivo" @click="removeFile(index)"><X :size="15" /></button></div></div>
        <button class="btn-primary upload-button" type="button" :disabled="!files.length || loading" @click="upload"><UploadCloud :size="17" />{{ loading && progress.total ? `Importando ${progress.current + 1} de ${progress.total}…` : 'Importar archivos validados' }}</button>
        <p class="privacy">Los archivos se procesan en memoria dentro de la API; no se ejecutan ni se publican.</p>
      </article>

      <article class="panel preview-card">
        <template v-if="preview">
          <div class="preview-head"><div><p class="eyebrow">Vista previa</p><h3>{{ preview.fileName }}</h3></div><StatusPill :label="preview.exportType" tone="success" /></div>
          <div class="format-grid"><span><small>Codificación</small><strong>{{ preview.encoding }}</strong></span><span><small>Delimitador</small><strong>{{ preview.delimiter }}</strong></span><span><small>Muestra válida</small><strong>{{ preview.validRows }}/{{ preview.sampledRows }}</strong></span></div>
          <div v-if="preview.rows.length" class="preview-table"><table><thead><tr><th v-for="column in previewColumns" :key="column">{{ column }}</th></tr></thead><tbody><tr v-for="(row, index) in preview.rows.slice(0, 5)" :key="index"><td v-for="column in previewColumns" :key="column" :title="row[column]">{{ row[column] || '—' }}</td></tr></tbody></table></div>
          <p v-if="preview.warnings.length" class="preview-warning"><AlertTriangle :size="15" />{{ preview.warnings.join(' · ') }}</p>
        </template>
        <EmptyState v-else title="Vista previa pendiente" description="Al seleccionar archivos se validarán las primeras filas antes de importar." />
      </article>
    </section>

    <section v-if="results.length" class="panel results-card"><div class="card-head"><span><CheckCircle2 :size="20" /></span><div><h3>Resultado del lote</h3><p>{{ results.length }} archivo(s) procesado(s)</p></div></div><div class="result-list"><div v-for="item in results" :key="item.id"><div><strong>{{ item.fileName }}</strong><StatusPill :label="item.status" :tone="tone(item.status)" /></div><dl><span><dt>Procesadas</dt><dd>{{ formatNumber(item.rowsProcessed) }}</dd></span><span><dt>Insertadas</dt><dd>{{ formatNumber(item.rowsInserted) }}</dd></span><span><dt>Repetidas</dt><dd>{{ formatNumber(item.rowsDuplicate) }}</dd></span><span><dt>Rechazadas</dt><dd>{{ formatNumber(item.rowsRejected) }}</dd></span></dl><p v-if="item.warnings">{{ item.warnings }}</p></div></div></section>

    <section class="panel history-card">
      <div class="card-head history-head"><span><History :size="20" /></span><div><h3>Importaciones recientes</h3><p>Trazabilidad de los últimos 50 trabajos</p></div></div>
      <EmptyState v-if="!jobs.length" title="No hay importaciones registradas" description="Seleccione un CSV para crear el primer trabajo histórico." />
      <div v-else class="jobs"><div v-for="job in jobs" :key="job.id" class="job"><FileSpreadsheet :size="19" /><div class="job-name"><strong>{{ job.fileName }}</strong><span>{{ formatDateTime(job.startedAtUtc) }}</span></div><div class="job-counts"><span>{{ formatNumber(job.rowsInserted) }} insertadas</span><span>{{ formatNumber(job.rowsDuplicate) }} repetidas</span><span v-if="job.rowsRejected">{{ formatNumber(job.rowsRejected) }} rechazadas</span></div><StatusPill :label="job.status" :tone="tone(job.status)" /></div></div>
    </section>
  </div>
</template>

<style scoped>
.imports{display:grid;gap:15px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.notice{display:flex;align-items:flex-start;gap:10px;border:1px solid #bfdbfe;border-radius:12px;padding:11px 13px;color:#1e3a8a;background:#eff6ff}.notice p{margin:0;font-size:12px;line-height:1.55}.import-grid{display:grid;grid-template-columns:.8fr 1.2fr;gap:14px}.upload-card,.preview-card,.results-card,.history-card{padding:18px}.card-head{display:flex;align-items:center;gap:11px}.card-head>span{display:grid;place-items:center;width:38px;height:38px;border-radius:10px;color:#1d4ed8;background:#eaf2ff}.card-head h3{margin:0;font-size:15px}.card-head p{margin:3px 0 0;color:#64748b;font-size:11px}.dropzone{display:grid;place-items:center;min-height:180px;margin-top:16px;border:2px dashed #93c5fd;border-radius:14px;padding:24px;color:#3b82f6;background:#f7fbff;text-align:center}.dropzone.active,.dropzone:hover{border-color:#1d4ed8;background:#eff6ff}.dropzone input{position:absolute;width:1px;height:1px;opacity:0}.dropzone strong{margin-top:11px;color:#1e3a8a}.dropzone span{margin-top:4px;color:#64748b;font-size:12px}.file-list{display:grid;gap:6px;margin-top:10px}.file-list>div{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:8px;border:1px solid #e2e8f0;border-radius:9px;padding:8px;color:#64748b}.file-list span strong,.file-list span small{display:block}.file-list span strong{max-width:310px;overflow:hidden;color:#334155;font-size:11px;text-overflow:ellipsis;white-space:nowrap}.file-list span small{font-size:9px}.file-list button{border:0;color:#64748b;background:transparent}.upload-button{width:100%;margin-top:12px}.privacy{margin:10px 0 0;color:#94a3b8;font-size:10px;text-align:center}.preview-card{min-width:0}.preview-head{display:flex;align-items:flex-start;justify-content:space-between;gap:10px}.preview-head h3{margin:0;max-width:350px;overflow:hidden;font-size:15px;text-overflow:ellipsis;white-space:nowrap}.format-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin:17px 0}.format-grid span{border:1px solid #dbeafe;border-radius:9px;padding:9px;background:#f8fbff}.format-grid small,.format-grid strong{display:block}.format-grid small{color:#64748b;font-size:9px;text-transform:uppercase}.format-grid strong{margin-top:3px;font-size:12px}.preview-table{overflow:auto;border:1px solid #e2e8f0;border-radius:10px}.preview-table table{width:100%;border-collapse:collapse;font-size:10px}.preview-table th{padding:8px;color:#3b5f83;background:#eaf4fc;text-align:left}.preview-table td{max-width:160px;overflow:hidden;border-top:1px solid #edf2f7;padding:8px;text-overflow:ellipsis;white-space:nowrap}.preview-warning{display:flex;gap:6px;margin:10px 0 0;color:#92400e;font-size:10px}.results-card>.card-head{margin-bottom:13px}.result-list{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}.result-list>div{border:1px solid #dbeafe;border-radius:11px;padding:12px}.result-list>div>div{display:flex;align-items:center;justify-content:space-between;gap:10px}.result-list>div>div strong{overflow:hidden;font-size:12px;text-overflow:ellipsis;white-space:nowrap}.result-list dl{display:grid;grid-template-columns:repeat(4,1fr);gap:6px;margin:12px 0 0}.result-list dt{color:#64748b;font-size:8px;text-transform:uppercase}.result-list dd{margin:2px 0 0;font-size:15px;font-weight:800}.result-list p{max-height:70px;overflow:auto;margin:10px 0 0;color:#92400e;font-size:9px;white-space:pre-line}.history-head{margin-bottom:13px}.jobs{display:grid}.job{display:grid;grid-template-columns:auto minmax(180px,1fr) auto auto;align-items:center;gap:13px;border-top:1px solid #e5edf5;padding:11px 3px;color:#64748b}.job-name strong,.job-name span{display:block}.job-name strong{color:#334155;font-size:13px}.job-name span,.job-counts{margin-top:2px;color:#64748b;font-size:10px}.job-counts{display:flex;gap:10px}
@media(max-width:950px){.import-grid{grid-template-columns:1fr}.result-list{grid-template-columns:1fr}.job{grid-template-columns:auto 1fr auto}.job-counts{display:none}}@media(max-width:520px){.job{grid-template-columns:auto 1fr}.job>:last-child{grid-column:2}.notice{flex-direction:column}.format-grid{grid-template-columns:1fr}.result-list dl{grid-template-columns:repeat(2,1fr)}}
</style>
