<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { CalendarDays, Monitor, Search, Share2, UserRound, UsersRound } from '@lucide/vue'
import DataModeSwitch from '../components/common/DataModeSwitch.vue'
import EmptyState from '../components/common/EmptyState.vue'
import ErrorBanner from '../components/common/ErrorBanner.vue'
import LoadingState from '../components/common/LoadingState.vue'
import EventTimeline from '../components/events/EventTimeline.vue'
import { apiRequest } from '../services/api'
import { usePreferencesStore } from '../stores/preferences'
import type { PagedResult, SmbUserActivity, SmbUserSummary } from '../types/api'
import { actionLabels, formatDateTime, formatNumber } from '../utils/format'

const preferences = usePreferencesStore()
const users = ref<PagedResult<SmbUserSummary> | null>(null)
const profile = ref<SmbUserActivity | null>(null)
const search = ref('')
const period = ref('30')
const loading = ref(false)
const detailLoading = ref(false)
const error = ref('')
const range = computed(() => {
  const to = new Date(); const from = new Date(to); from.setDate(from.getDate() - Number.parseInt(period.value))
  return { from: from.toISOString(), to: to.toISOString() }
})
const dailyRows = computed(() => profile.value?.activityByDay.slice(-14) ?? [])
const dailyMax = computed(() => Math.max(1, ...dailyRows.value.map(item => item.count)))

async function load() {
  loading.value = true; error.value = ''
  try {
    const params = new URLSearchParams({ ...range.value, mode: preferences.dataMode, pageSize: '100' })
    if (search.value.trim()) params.set('search', search.value.trim())
    users.value = await apiRequest<PagedResult<SmbUserSummary>>(`/api/v1/smb-users?${params}`)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'No fue posible cargar las cuentas SMB.' }
  finally { loading.value = false }
}

async function openUser(user: string) {
  detailLoading.value = true; error.value = ''
  try {
    const params = new URLSearchParams({ ...range.value, mode: preferences.dataMode })
    profile.value = await apiRequest<SmbUserActivity>(`/api/v1/smb-users/${encodeURIComponent(user)}/activity?${params}`)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'No fue posible cargar la actividad de la cuenta.' }
  finally { detailLoading.value = false }
}

watch([period, () => preferences.dataMode], () => { profile.value = null; void load() })
onMounted(load)
</script>

<template>
  <div class="users-page">
    <section class="page-head"><div><p class="eyebrow">Identidades observadas</p><h2>Cuentas SMB</h2><p>Las cuentas técnicas no se equiparan automáticamente con personas.</p></div><div class="head-actions"><DataModeSwitch v-model="preferences.dataMode" /><select v-model="period" class="field"><option value="7">7 días</option><option value="30">30 días</option><option value="90">90 días</option><option value="365">365 días</option></select></div></section>
    <section class="panel search-panel"><div><Search :size="17" /><input v-model="search" class="field" placeholder="Buscar cuenta SMB" @keyup.enter="load" /></div><button class="btn-primary" type="button" @click="load">Buscar</button></section>
    <ErrorBanner v-if="error" :message="error" @retry="load" />

    <div class="workspace">
      <section class="panel list-panel">
        <div class="panel-head"><UsersRound :size="18" /><div><strong>{{ formatNumber(users?.totalItems ?? 0) }} cuentas</strong><span>Actividad observada en los últimos {{ period }} días</span></div></div>
        <LoadingState v-if="loading && !users" />
        <EmptyState v-else-if="users?.items.length === 0" title="No hay cuentas en este origen" description="Cambie el modo de datos o la búsqueda." />
        <button v-for="item in users?.items" :key="item.user" class="user-row" :class="{ active: profile?.summary.user === item.user }" type="button" @click="openUser(item.user)">
          <span class="avatar">{{ item.user.slice(0, 2).toUpperCase() }}</span>
          <span><strong>{{ item.user }}</strong><small>{{ item.lastClientIp ?? 'IP no informada' }} · {{ item.shareCount }} recurso(s)</small></span>
          <span class="total">{{ formatNumber(item.eventCount) }}</span>
          <time>{{ formatDateTime(item.lastSeenUtc) }}</time>
        </button>
      </section>

      <section class="profile">
        <LoadingState v-if="detailLoading" class="panel" />
        <EmptyState v-else-if="!profile" class="panel" title="Seleccione una cuenta" :description="`Verá sus IP observadas, recursos, distribución de acciones y eventos recientes de los últimos ${period} días.`" />
        <template v-else>
          <article class="panel identity"><div class="large-avatar"><UserRound :size="27" /></div><div><p class="eyebrow">Perfil técnico SMB</p><h3>{{ profile.summary.user }}</h3><span>Última actividad: {{ formatDateTime(profile.summary.lastSeenUtc) }}</span></div></article>
          <section class="facts"><article class="panel"><strong>{{ formatNumber(profile.summary.eventCount) }}</strong><span>Eventos · {{ period }} días</span></article><article class="panel"><strong>{{ profile.summary.ipCount }}</strong><span>Direcciones IP</span></article><article class="panel"><strong>{{ profile.summary.shareCount }}</strong><span>Recursos</span></article></section>
          <section class="breakdown">
            <article class="panel"><div class="card-title"><Monitor :size="17" />IP observadas</div><div class="bars"><div v-for="item in profile.clientIps" :key="item.name"><span>{{ item.name }}</span><strong>{{ formatNumber(item.count) }}</strong></div></div></article>
            <article class="panel"><div class="card-title"><Share2 :size="17" />Recursos</div><div class="bars"><div v-for="item in profile.shares" :key="item.name"><span>{{ item.name }}</span><strong>{{ formatNumber(item.count) }}</strong></div></div></article>
            <article class="panel"><div class="card-title"><UsersRound :size="17" />Acciones</div><div class="bars"><div v-for="item in profile.actions" :key="item.name"><span>{{ actionLabels[item.name] ?? item.name }}</span><strong>{{ formatNumber(item.count) }}</strong></div></div></article>
          </section>
          <article class="panel daily"><div class="card-title"><CalendarDays :size="17" />Actividad diaria · últimos 14 días con eventos</div><div class="daily-bars"><div v-for="item in dailyRows" :key="item.dayUtc"><time>{{ new Date(item.dayUtc).toLocaleDateString('es-CL', { day: '2-digit', month: 'short', timeZone: 'UTC' }) }}</time><span><i :style="{ width: `${Math.max(3, item.count / dailyMax * 100)}%` }" /></span><strong>{{ formatNumber(item.count) }}</strong></div></div></article>
          <article class="panel recent"><div class="card-title">Eventos recientes</div><EventTimeline :events="profile.recentEvents" /></article>
        </template>
      </section>
    </div>
  </div>
</template>

<style scoped>
.users-page{display:grid;gap:15px}.page-head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px}.page-head h2{margin:0;font-size:24px}.page-head p:not(.eyebrow){margin:5px 0 0;color:#64748b;font-size:13px}.head-actions{display:flex;gap:8px}.head-actions select{width:110px}.eyebrow{margin:0 0 5px;color:#1d4ed8;font-size:10px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.search-panel{display:flex;gap:9px;padding:14px}.search-panel>div{position:relative;flex:1}.search-panel svg{position:absolute;left:12px;top:13px;color:#94a3b8}.search-panel input{padding-left:38px}.workspace{display:grid;grid-template-columns:minmax(340px,.78fr) minmax(0,1.5fr);gap:14px;align-items:start}.list-panel{overflow:hidden}.panel-head{display:flex;align-items:center;gap:10px;padding:14px 15px;border-bottom:1px solid #dbeafe;color:#1d4ed8}.panel-head strong,.panel-head span{display:block}.panel-head strong{color:#0f172a;font-size:13px}.panel-head span{margin-top:2px;color:#64748b;font-size:10px}.user-row{display:grid;width:100%;grid-template-columns:auto 1fr auto;gap:10px;align-items:center;border:0;border-bottom:1px solid #edf2f7;padding:11px 14px;background:#fff;text-align:left}.user-row:hover,.user-row.active{background:#eff6ff}.user-row.active{box-shadow:inset 3px 0 #2563eb}.avatar{display:grid;width:34px;height:34px;place-items:center;border-radius:10px;color:#1d4ed8;background:#dbeafe;font-size:10px;font-weight:800}.user-row strong,.user-row small{display:block}.user-row strong{font-size:12px}.user-row small{margin-top:3px;color:#64748b;font-size:10px}.total{border-radius:999px;padding:3px 7px;color:#1e40af;background:#dbeafe;font-size:10px;font-weight:800}.user-row time{grid-column:2 / -1;color:#94a3b8;font-size:9px}.profile{display:grid;gap:12px}.identity{display:flex;align-items:center;gap:13px;padding:16px}.large-avatar{display:grid;width:50px;height:50px;place-items:center;border-radius:14px;color:#1d4ed8;background:#dbeafe}.identity h3{margin:0;font-size:18px}.identity span{color:#64748b;font-size:11px}.facts{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.facts article{padding:14px}.facts strong,.facts span{display:block}.facts strong{font-size:20px}.facts span{margin-top:3px;color:#64748b;font-size:10px}.breakdown{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.breakdown article,.recent,.daily{padding:14px}.card-title{display:flex;align-items:center;gap:7px;margin-bottom:10px;color:#1e3a8a;font-size:12px;font-weight:800}.bars{display:grid;gap:7px}.bars div{display:flex;justify-content:space-between;gap:10px;border-bottom:1px solid #edf2f7;padding-bottom:6px;font-size:10px}.bars span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.bars strong{color:#1d4ed8}.daily-bars{display:grid;gap:7px}.daily-bars>div{display:grid;grid-template-columns:60px 1fr 45px;gap:8px;align-items:center;font-size:9px}.daily-bars span{height:7px;overflow:hidden;border-radius:999px;background:#e2e8f0}.daily-bars i{display:block;height:100%;border-radius:999px;background:#2563eb}.daily-bars strong{text-align:right;color:#1d4ed8}@media(max-width:1150px){.workspace{grid-template-columns:1fr}.breakdown{grid-template-columns:1fr 1fr 1fr}}@media(max-width:720px){.page-head{align-items:flex-start;flex-direction:column}.head-actions{width:100%;flex-wrap:wrap}.breakdown,.facts{grid-template-columns:1fr}.search-panel{align-items:stretch;flex-direction:column}}
</style>
