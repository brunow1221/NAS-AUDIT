<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { AlertTriangle, CheckCircle2, Clock3, Database, Download, HardDrive, RefreshCw, ServerCog, Trash2, UserPlus, UsersRound, Wifi } from '@lucide/vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import StatusPill from '../components/common/StatusPill.vue'
import PasswordForm from '../components/settings/PasswordForm.vue'
import { apiRequest } from '../services/api'
import { useAuthStore } from '../stores/auth'
import type { ApplicationUser, RetentionPreview, SystemStatus } from '../types/api'
import { formatDateTime, formatNumber } from '../utils/format'

const auth = useAuthStore()
const status = ref<SystemStatus | null>(null)
const retention = ref<RetentionPreview | null>(null)
const applicationUsers = ref<ApplicationUser[]>([])
const loading = ref(false)
const error = ref('')
const retentionConfirmation = ref('')
const runningRetention = ref(false)
const creatingUser = ref(false)
const newUser = ref({ email: '', displayName: '', role: 'Auditor', temporaryPassword: '' })
const temporaryPasswords = ref<Record<string, string>>({})
const userMessage = ref('')
let timer: number | undefined

const criticalAlerts = computed(() => status.value?.alerts.filter(item => item.severity === 'critical').length ?? 0)
const retentionEligible = computed(() => (retention.value?.events.reduce((sum, item) => sum + item.eligibleRows, 0) ?? 0) + (retention.value?.eligibleIngestionBatches ?? 0) + (retention.value?.eligibleRejectedEvents ?? 0))

function bytes(value: number): string {
  if (!value) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']; const index = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1)
  return `${(value / 1024 ** index).toLocaleString('es-CL', { maximumFractionDigits: 1 })} ${units[index]}`
}

async function load() {
  if (auth.user?.mustChangePassword) {
    status.value = null; retention.value = null; applicationUsers.value = []; loading.value = false; error.value = ''
    return
  }
  loading.value = true; error.value = ''
  try {
    const statusRequest = apiRequest<SystemStatus>('/api/v1/system/status')
    const canLoadAdministration = auth.isAdministrator
    const retentionRequest = canLoadAdministration ? apiRequest<RetentionPreview>('/api/v1/admin/retention/preview') : Promise.resolve(null)
    const usersRequest = canLoadAdministration ? apiRequest<ApplicationUser[]>('/api/v1/admin/users') : Promise.resolve([])
    const [nextStatus, nextRetention, nextUsers] = await Promise.all([statusRequest, retentionRequest, usersRequest])
    status.value = nextStatus; retention.value = nextRetention; applicationUsers.value = nextUsers
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'No fue posible consultar el estado operativo.' }
  finally { loading.value = false }
}

function startPolling() {
  if (timer === undefined) timer = window.setInterval(load, 30_000)
}

async function onPasswordChanged() {
  await load()
  startPolling()
}

async function runRetention() {
  if (retentionConfirmation.value !== 'APLICAR' || !retention.value?.policy.enabled) return
  if (!window.confirm(`Se eliminarán ${formatNumber(retentionEligible.value)} registros que exceden la política. ¿Continuar?`)) return
  runningRetention.value = true; error.value = ''
  try {
    await apiRequest('/api/v1/admin/retention/run', { method: 'POST' })
    retentionConfirmation.value = ''; await load()
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'La retención no pudo completarse.' }
  finally { runningRetention.value = false }
}

async function createUser() {
  creatingUser.value = true; userMessage.value = ''
  try {
    await apiRequest('/api/v1/admin/users', { method: 'POST', body: JSON.stringify(newUser.value) })
    newUser.value = { email: '', displayName: '', role: 'Auditor', temporaryPassword: '' }
    userMessage.value = 'Cuenta creada. Entregue la contraseña temporal por un canal seguro.'; await load()
  } catch (reason) { userMessage.value = reason instanceof Error ? reason.message : 'No fue posible crear la cuenta.' }
  finally { creatingUser.value = false }
}

async function setUserEnabled(user: ApplicationUser) {
  userMessage.value = ''
  try {
    await apiRequest(`/api/v1/admin/users/${user.id}/enabled`, { method: 'PUT', body: JSON.stringify({ isEnabled: !user.isEnabled }) }); await load()
  } catch (reason) { userMessage.value = reason instanceof Error ? reason.message : 'No fue posible actualizar la cuenta.' }
}

async function setUserRole(user: ApplicationUser, role: string) {
  userMessage.value = ''
  try {
    await apiRequest(`/api/v1/admin/users/${user.id}/role`, { method: 'PUT', body: JSON.stringify({ role }) }); await load()
  } catch (reason) { userMessage.value = reason instanceof Error ? reason.message : 'No fue posible actualizar el rol.' }
}

function roleFromEvent(user: ApplicationUser, event: Event) {
  void setUserRole(user, (event.target as HTMLSelectElement).value)
}

async function resetUserPassword(user: ApplicationUser) {
  const temporaryPassword = temporaryPasswords.value[user.id]
  if (!temporaryPassword) return
  userMessage.value = ''
  try {
    await apiRequest(`/api/v1/admin/users/${user.id}/reset-password`, { method: 'POST', body: JSON.stringify({ temporaryPassword }) })
    temporaryPasswords.value[user.id] = ''
    userMessage.value = 'Contraseña temporal asignada; la cuenta deberá cambiarla en su próximo acceso.'; await load()
  } catch (reason) { userMessage.value = reason instanceof Error ? reason.message : 'No fue posible restablecer la contraseña.' }
}

onMounted(() => {
  if (!auth.user?.mustChangePassword) { void load(); startPolling() }
})
onBeforeUnmount(() => { if (timer !== undefined) window.clearInterval(timer) })
</script>

<template>
  <div class="settings-page">
    <section class="page-head"><div><p class="eyebrow">Operación continua</p><h2>Seguridad y estado del sistema</h2><p>Salud consolidada, políticas de conservación y controles administrativos.</p></div><button v-if="!auth.user?.mustChangePassword" class="btn-secondary" :disabled="loading" @click="load"><RefreshCw :size="16" />Actualizar</button></section>
    <section v-if="auth.user?.mustChangePassword" class="password-required"><PasswordForm @changed="onPasswordChanged" /></section>
    <ErrorBanner v-if="error" :message="error" @retry="load" />
    <LoadingState v-if="loading && !status" />
    <template v-else-if="status">
      <section class="health-grid">
        <article class="panel health"><ServerCog :size="22" /><div><span>API</span><strong>Operativa · v{{ status.version }}</strong><small>{{ Math.floor(status.uptimeSeconds / 60) }} minutos de actividad</small></div><StatusPill label="operativa" tone="success" /></article>
        <article class="panel health"><Database :size="22" /><div><span>PostgreSQL</span><strong>{{ status.databaseStatus === 'operational' ? 'Operativo' : 'No disponible' }}</strong><small>{{ bytes(status.databaseSizeBytes) }} · {{ status.databaseLatencyMs.toFixed(1) }} ms</small></div><StatusPill :label="status.databaseStatus" :tone="status.databaseStatus === 'operational' ? 'success' : 'danger'" /></article>
        <article class="panel health"><Wifi :size="22" /><div><span>Recolector</span><strong>{{ status.collector.label }}</strong><small>Heartbeat {{ formatDateTime(status.collector.lastHeartbeatUtc) }}</small></div><StatusPill :label="status.collector.status" :tone="['connected','active_no_events'].includes(status.collector.status) ? 'success' : 'warning'" /></article>
      </section>

      <section class="metrics"><article class="panel"><HardDrive :size="19" /><div><strong>{{ formatNumber(status.totalEvents) }}</strong><span>Eventos conservados</span></div></article><article class="panel"><Download :size="19" /><div><strong>{{ formatNumber(status.ingestion.acceptedLast24Hours) }}</strong><span>Aceptados · 24 h</span></div></article><article class="panel"><AlertTriangle :size="19" /><div><strong>{{ status.ingestion.rejectedLast24Hours }}</strong><span>Rechazados · 24 h</span></div></article><article class="panel"><Clock3 :size="19" /><div><strong>{{ status.ingestion.pendingEvents }}</strong><span>Pendientes en cola</span></div></article></section>

      <section class="panel alerts"><div class="card-head"><div><h3>Observaciones operativas</h3><p>Umbrales explícitos para revisión humana; no califican actividad como maliciosa.</p></div><StatusPill :label="status.alerts.length ? `${status.alerts.length} observación(es)` : 'Sin observaciones'" :tone="criticalAlerts ? 'danger' : status.alerts.length ? 'warning' : 'success'" /></div><div v-if="status.alerts.length" class="alert-list"><article v-for="item in status.alerts" :key="item.ruleKey" :class="item.severity"><AlertTriangle :size="18" /><div><strong>{{ item.title }}</strong><span>{{ item.explanation }}</span><small>{{ formatNumber(item.observedCount) }} observación(es) · {{ formatDateTime(item.windowEndUtc) }}</small></div></article></div><div v-else class="all-good"><CheckCircle2 :size="19" />No hay observaciones operativas activas.</div></section>

      <section class="two-columns">
        <PasswordForm @changed="onPasswordChanged" />

        <article class="panel backups"><div class="card-title"><HardDrive :size="18" />Respaldo y recuperación</div><p>Las copias se ejecutan desde SSH para que la aplicación web nunca pueda invocar comandos del NAS.</p><div><strong>Respaldo verificado</strong><span><code>sudo ./scripts/backup.sh</code> genera dump, SHA-256 y manifiesto.</span></div><div><strong>Prueba aislada</strong><span><code>sudo ./scripts/verify-backup.sh ARCHIVO.dump</code> restaura en un contenedor temporal.</span></div><div><strong>Restauración controlada</strong><span>Requiere confirmación explícita y crea una copia previa automática.</span></div></article>
      </section>

      <section v-if="auth.isAdministrator" class="panel app-users">
        <div class="card-head"><div><h3>Cuentas de la aplicación</h3><p>Acceso local al portal; son independientes de las cuentas SMB observadas.</p></div><UsersRound :size="20" /></div>
        <div class="users-layout">
          <form class="new-user" @submit.prevent="createUser">
            <div class="card-title"><UserPlus :size="17" />Nueva cuenta</div>
            <label class="label">Nombre visible<input v-model.trim="newUser.displayName" class="field" maxlength="256" required /></label>
            <label class="label">Correo<input v-model.trim="newUser.email" class="field" type="email" maxlength="256" required /></label>
            <label class="label">Rol<select v-model="newUser.role" class="field"><option value="Auditor">Auditor</option><option value="Administrator">Administrador</option></select></label>
            <label class="label">Contraseña temporal<input v-model="newUser.temporaryPassword" class="field" type="password" minlength="14" autocomplete="new-password" required /></label>
            <button class="btn-primary" :disabled="creatingUser" type="submit">{{ creatingUser ? 'Creando…' : 'Crear cuenta' }}</button>
            <span v-if="userMessage" class="form-message">{{ userMessage }}</span>
          </form>
          <div class="user-list">
            <article v-for="item in applicationUsers" :key="item.id">
              <div class="user-main"><span class="user-avatar">{{ item.displayName.slice(0, 2).toUpperCase() }}</span><div><strong>{{ item.displayName }}</strong><span>{{ item.email }}</span><small>Último acceso: {{ formatDateTime(item.lastLoginAtUtc) }}</small></div></div>
              <StatusPill :label="item.isEnabled ? 'habilitada' : 'deshabilitada'" :tone="item.isEnabled ? 'success' : 'neutral'" />
              <select class="field role" :value="item.role" @change="roleFromEvent(item, $event)"><option value="Auditor">Auditor</option><option value="Administrator">Administrador</option></select>
              <input v-model="temporaryPasswords[item.id]" class="field temporary-password" type="password" minlength="14" autocomplete="new-password" placeholder="Nueva clave temporal" />
              <button class="btn-secondary" type="button" :disabled="(temporaryPasswords[item.id]?.length ?? 0) < 14" @click="resetUserPassword(item)">Asignar clave</button>
              <button class="btn-secondary" type="button" @click="setUserEnabled(item)">{{ item.isEnabled ? 'Deshabilitar' : 'Habilitar' }}</button>
              <small v-if="item.mustChangePassword" class="must-change">Cambio de contraseña pendiente</small>
            </article>
          </div>
        </div>
      </section>

      <section v-if="auth.isAdministrator && retention" class="panel retention"><div class="card-head"><div><h3>Política de retención</h3><p :class="{ disabled: !retention.policy.enabled }">{{ retention.policy.enabled ? `Activa · ejecución diaria a las ${retention.policy.runAtUtcHour}:00 UTC` : 'Desactivada hasta aprobación formal de TI' }}</p></div><StatusPill :label="retention.policy.enabled ? 'activa' : 'desactivada'" :tone="retention.policy.enabled ? 'success' : 'neutral'" /></div><div class="policy-grid"><div v-for="item in retention.events" :key="item.dataMode"><span>{{ item.dataMode }}</span><strong>{{ formatNumber(item.eligibleRows) }}</strong><small>anteriores a {{ formatDateTime(item.cutoffUtc) }}</small></div><div><span>Lotes de ingesta</span><strong>{{ formatNumber(retention.eligibleIngestionBatches) }}</strong><small>metadatos elegibles</small></div><div><span>Eventos rechazados</span><strong>{{ formatNumber(retention.eligibleRejectedEvents) }}</strong><small>evidencia técnica elegible</small></div></div><div class="retention-action"><Trash2 :size="18" /><div><strong>{{ formatNumber(retentionEligible) }} registros elegibles</strong><span>La ejecución es irreversible y sólo se habilita cuando <code>RETENTION_ENABLED=true</code>.</span></div><input v-model="retentionConfirmation" class="field" placeholder="Escriba APLICAR" :disabled="!retention.policy.enabled" /><button class="btn-danger" :disabled="!retention.policy.enabled || retentionConfirmation !== 'APLICAR' || runningRetention" @click="runRetention">{{ runningRetention ? 'Aplicando…' : 'Aplicar ahora' }}</button></div></section>

      <section v-if="status.recentErrors.length" class="panel recent-errors"><div class="card-title">Errores recientes de esta instancia</div><div v-for="item in status.recentErrors" :key="`${item.timestampUtc}-${item.code}`"><time>{{ formatDateTime(item.timestampUtc) }}</time><code>{{ item.code }}</code><span>{{ item.message }}</span></div></section>
    </template>
  </div>
</template>

<style scoped>
.settings-page{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:18px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.password-required{max-width:560px}.health-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.health{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:11px;padding:15px;color:#2563eb}.health span,.health strong,.health small{display:block}.health span{color:#64748b;font-size:9px;text-transform:uppercase}.health strong{color:#0f172a;font-size:12px}.health small{margin-top:3px;color:#64748b;font-size:9px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.metrics article{display:flex;align-items:center;gap:10px;padding:14px;color:#1d4ed8}.metrics strong,.metrics span{display:block}.metrics strong{color:#0f172a;font-size:19px}.metrics span{color:#64748b;font-size:9px}.alerts,.retention,.backups,.recent-errors{padding:16px}.card-head{display:flex;align-items:start;justify-content:space-between;gap:12px}.card-head h3{margin:0;font-size:14px}.card-head p{margin:4px 0 0;color:#64748b;font-size:10px}.alert-list{display:grid;gap:7px;margin-top:13px}.alert-list article{display:flex;gap:9px;border-left:3px solid;padding:10px;border-radius:7px;background:#f8fafc}.alert-list article.critical{border-color:#dc2626;color:#b91c1c}.alert-list article.warning{border-color:#f59e0b;color:#92400e}.alert-list article.info{border-color:#3b82f6;color:#1d4ed8}.alert-list strong,.alert-list span,.alert-list small{display:block}.alert-list strong{font-size:11px}.alert-list span{margin-top:2px;color:#475569;font-size:10px}.alert-list small{margin-top:4px;color:#94a3b8;font-size:9px}.all-good{display:flex;align-items:center;gap:8px;margin-top:13px;color:#15803d;font-size:11px}.two-columns{display:grid;grid-template-columns:1fr 1fr;gap:14px}.card-title{display:flex;align-items:center;gap:7px;color:#1e3a8a;font-size:13px;font-weight:800}.backups>p{color:#64748b;font-size:10px}.backups>div:not(.card-title){border-top:1px solid #edf2f7;padding:10px 0}.backups strong,.backups span{display:block}.backups strong{font-size:11px}.backups span{margin-top:3px;color:#64748b;font-size:10px;line-height:1.5}.backups code,.retention code{color:#1e3a8a;background:#eff6ff}.policy-grid{display:grid;grid-template-columns:repeat(5,1fr);gap:8px;margin-top:13px}.policy-grid>div{border:1px solid #dbeafe;border-radius:9px;padding:10px}.policy-grid span,.policy-grid strong,.policy-grid small{display:block}.policy-grid span{color:#64748b;font-size:9px}.policy-grid strong{margin:4px 0;font-size:17px}.policy-grid small{color:#94a3b8;font-size:8px}.disabled{color:#92400e!important}.retention-action{display:grid;grid-template-columns:auto 1fr 180px auto;align-items:center;gap:10px;border-top:1px solid #fee2e2;margin-top:13px;padding-top:13px;color:#b91c1c}.retention-action strong,.retention-action span{display:block}.retention-action strong{font-size:11px}.retention-action span{color:#64748b;font-size:9px}.btn-danger{border:0;border-radius:9px;padding:10px 12px;color:#fff;background:#dc2626;font-weight:800}.btn-danger:disabled{opacity:.45}.recent-errors>div:not(.card-title){display:grid;grid-template-columns:150px 140px 1fr;gap:8px;border-top:1px solid #edf2f7;padding:8px 0;font-size:9px}.recent-errors time{color:#64748b}@media(max-width:1100px){.health-grid,.two-columns{grid-template-columns:1fr}.metrics{grid-template-columns:1fr 1fr}.policy-grid{grid-template-columns:repeat(2,1fr)}.retention-action{grid-template-columns:auto 1fr}.retention-action input,.retention-action button{grid-column:2}}@media(max-width:650px){.page-head{align-items:flex-start;flex-direction:column}.metrics,.policy-grid{grid-template-columns:1fr}.health{grid-template-columns:auto 1fr}.health>:last-child{grid-column:2}.recent-errors>div:not(.card-title){grid-template-columns:1fr}.retention-action{grid-template-columns:1fr}.retention-action>*{grid-column:auto!important}}
</style>

<style scoped>
.app-users { padding: 16px; }
.users-layout { display: grid; grid-template-columns: 300px 1fr; gap: 16px; margin-top: 14px; }
.new-user { display: grid; align-content: start; gap: 10px; border: 1px solid #dbeafe; border-radius: 10px; padding: 13px; }
.new-user .label { display: grid; gap: 4px; }
.user-list { display: grid; align-content: start; gap: 8px; }
.user-list > article { display: grid; grid-template-columns: minmax(220px, 1fr) auto 130px minmax(150px, 190px) auto auto; align-items: center; gap: 8px; border: 1px solid #e2e8f0; border-radius: 10px; padding: 10px; }
.user-main { display: flex; align-items: center; gap: 9px; min-width: 0; }
.user-main div { min-width: 0; }
.user-main strong, .user-main span, .user-main small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.user-main strong { font-size: 11px; }
.user-main span, .user-main small { color: #64748b; font-size: 9px; }
.user-avatar { display: grid !important; flex: 0 0 32px; width: 32px; height: 32px; place-items: center; border-radius: 50%; color: #1d4ed8 !important; background: #dbeafe; font-weight: 800; }
.user-list .role, .temporary-password { min-width: 0; }
.must-change { color: #92400e; font-size: 8px; }
@media (max-width: 1100px) {
  .users-layout { grid-template-columns: 1fr; }
  .user-list > article { grid-template-columns: minmax(190px, 1fr) auto 120px minmax(150px, 1fr); }
}
@media (max-width: 650px) {
  .user-list > article { grid-template-columns: 1fr; }
}
</style>
