<script setup lang="ts">
import { computed, ref } from 'vue'
import { Download, FileClock, FileSpreadsheet, FolderTree, Layers3, UsersRound } from '@lucide/vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import { downloadFile } from '../services/api'
import { usePreferencesStore } from '../stores/preferences'

const preferences = usePreferencesStore()
const period = ref('30')
const downloading = ref<string | null>(null)
const error = ref('')
const reports = [
  { kind: 'Events', title: 'Evidencia de eventos', description: 'Eventos técnicos con fecha, cuenta, IP, acción, ruta, origen y mensaje original.', icon: FileSpreadsheet },
  { kind: 'Users', title: 'Resumen de cuentas SMB', description: 'Volumen, IP observadas, recursos y período de actividad por cuenta.', icon: UsersRound },
  { kind: 'Paths', title: 'Inventario histórico de rutas', description: 'Rutas observadas, cantidad de eventos, cuentas y primera/última aparición.', icon: FolderTree },
  { kind: 'ActivityGroups', title: 'Grupos de actividad', description: 'Secuencias correlacionadas por cuenta, IP y ruta; no constituyen alertas de seguridad.', icon: Layers3 },
]
const range = computed(() => { const to = new Date(); const from = new Date(to); from.setDate(from.getDate() - Number.parseInt(period.value)); return { from: from.toISOString(), to: to.toISOString() } })

async function download(kind: string) {
  downloading.value = kind; error.value = ''
  try {
    const params = new URLSearchParams({ ...range.value, mode: preferences.dataMode })
    await downloadFile(`/api/v1/reports/${kind}?${params}`)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'No fue posible generar el informe.' }
  finally { downloading.value = null }
}
</script>

<template>
  <div class="reports-page">
    <section class="page-head"><div><p class="eyebrow">Evidencia exportable</p><h2>Informes</h2><p>Archivos CSV UTF-8 compatibles con análisis y conservación documental.</p></div><div class="actions"><DataModeSwitch v-model="preferences.dataMode" /><select v-model="period" class="field"><option value="7">Últimos 7 días</option><option value="30">Últimos 30 días</option><option value="90">Últimos 90 días</option><option value="365">Últimos 365 días</option></select></div></section>
    <ErrorBanner v-if="error" :message="error" />
    <section class="scope"><FileClock :size="18" /><div><strong>Alcance del informe</strong><span>{{ preferences.dataMode === 'Live' ? 'Eventos recolectados en vivo' : preferences.dataMode === 'Demo' ? 'Datos sintéticos aislados' : 'Historial importado' }} · {{ period }} días · Fechas almacenadas en UTC.</span></div></section>
    <section class="report-grid">
      <article v-for="report in reports" :key="report.kind" class="panel report-card">
        <div class="icon"><component :is="report.icon" :size="25" /></div>
        <div><h3>{{ report.title }}</h3><p>{{ report.description }}</p></div>
        <button class="btn-primary" type="button" :disabled="downloading !== null" @click="download(report.kind)"><Download :size="17" />{{ downloading === report.kind ? 'Generando…' : 'Descargar CSV' }}</button>
      </article>
    </section>
    <section class="panel notes"><h3>Controles aplicados</h3><ul><li>La exportación respeta el modo de datos y período elegidos.</li><li>Se neutralizan valores que podrían interpretarse como fórmulas de hoja de cálculo.</li><li>Los informes se generan desde PostgreSQL; no acceden a documentos empresariales.</li><li>El límite es de 100.000 filas por archivo. Si se supera, reduzca el período.</li><li>Una cuenta SMB o grupo de actividad no identifica por sí solo a una persona ni una conducta maliciosa.</li></ul></section>
  </div>
</template>

<style scoped>
.reports-page{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.actions{display:flex;gap:8px}.actions select{width:180px}.scope{display:flex;align-items:center;gap:10px;border:1px solid #bfdbfe;border-radius:12px;padding:11px 13px;color:#1d4ed8;background:#eff6ff}.scope strong,.scope span{display:block}.scope strong{font-size:11px}.scope span{margin-top:2px;color:#64748b;font-size:10px}.report-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.report-card{display:grid;grid-template-columns:auto 1fr;gap:13px;padding:18px}.report-card .icon{display:grid;width:49px;height:49px;place-items:center;border-radius:14px;color:#1d4ed8;background:#dbeafe}.report-card h3{margin:2px 0 5px;font-size:15px}.report-card p{margin:0;color:#64748b;font-size:11px;line-height:1.55}.report-card button{grid-column:1 / -1}.notes{padding:17px}.notes h3{margin:0 0 10px;font-size:14px}.notes ul{display:grid;gap:7px;margin:0;padding-left:18px;color:#475569;font-size:11px;line-height:1.45}@media(max-width:850px){.page-head{align-items:flex-start;flex-direction:column}.report-grid{grid-template-columns:1fr}.actions{width:100%;flex-wrap:wrap}.actions>*{flex:1}}@media(max-width:500px){.report-card{grid-template-columns:1fr}.report-card button{grid-column:auto}.report-card .icon{width:42px;height:42px}}
</style>
