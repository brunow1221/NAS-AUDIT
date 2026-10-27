<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Activity, BarChart3, Clock3, FileInput, FileText, FolderClock, Gauge, Settings, Users, X } from '@lucide/vue'
import BrandMark from './BrandMark.vue'
import { useAuthStore } from '../../stores/auth'

defineProps<{ open: boolean }>()
defineEmits<{ close: [] }>()
const route = useRoute()
const auth = useAuthStore()
const items = computed(() => [
  { to: '/dashboard', label: 'Dashboard', icon: Gauge },
  { to: '/live', label: 'Tiempo real', icon: Activity },
  { to: '/history', label: 'Historial', icon: Clock3 },
  { to: '/explorer', label: 'Explorador', icon: FolderClock },
  { to: '/users', label: 'Usuarios', icon: Users },
  { to: '/statistics', label: 'Estadísticas', icon: BarChart3 },
  { to: '/reports', label: 'Informes', icon: FileText },
  ...(auth.isAdministrator ? [{ to: '/imports', label: 'Importar CSV', icon: FileInput }] : []),
  { to: '/settings', label: 'Operación', icon: Settings },
])
</script>

<template>
  <div v-if="open" class="backdrop" @click="$emit('close')" />
  <aside class="sidebar" :class="{ 'sidebar--open': open }">
    <div class="brand">
      <BrandMark />
      <div><strong>BCMETALS</strong><span>NAS AUDIT</span></div>
      <button class="close" type="button" aria-label="Cerrar menú" @click="$emit('close')"><X :size="20" /></button>
    </div>
    <p class="section-label">Navegación</p>
    <nav aria-label="Navegación principal">
      <RouterLink
        v-for="item in items"
        :key="item.to"
        :to="item.to"
        class="nav-link"
        :class="{ active: route.path === item.to }"
        @click="$emit('close')"
      >
        <component :is="item.icon" :size="19" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>
    <div class="sidebar-note">
      <strong>Plataforma operativa</strong>
      <span>Fases 0–5 completadas</span>
    </div>
  </aside>
</template>

<style scoped>
.sidebar { position: fixed; inset: 0 auto 0 0; z-index: 40; display: flex; width: 252px; flex-direction: column; border-right: 1px solid #bfdbfe; background: #f8fbff; padding: 22px 16px 18px; }
.brand { display: flex; align-items: center; gap: 11px; padding: 0 6px 22px; }
.brand strong, .brand span { display: block; }
.brand strong { color: #0f2f68; font-size: 15px; letter-spacing: .05em; }
.brand span { margin-top: 1px; color: #3b82f6; font-size: 11px; font-weight: 800; letter-spacing: .18em; }
.close { display: none; margin-left: auto; border: 0; background: transparent; color: #64748b; }
.section-label { margin: 0 10px 9px; color: #94a3b8; font-size: 10px; font-weight: 800; letter-spacing: .14em; text-transform: uppercase; }
nav { display: grid; gap: 4px; }
.nav-link { position: relative; display: flex; min-height: 43px; align-items: center; gap: 11px; border-radius: 10px; padding: 9px 11px; color: #52647a; text-decoration: none; font-size: 14px; font-weight: 650; }
.nav-link:hover { color: #1e40af; background: #eff6ff; }
.nav-link.active { color: #fff; background: #1d4ed8; box-shadow: 0 7px 18px rgba(29, 78, 216, .22); }
.phase-dot { width: 6px; height: 6px; margin-left: auto; border-radius: 50%; background: #cbd5e1; }
.active .phase-dot { background: #bfdbfe; }
.sidebar-note { margin-top: auto; display: grid; gap: 3px; border: 1px solid #bfdbfe; border-radius: 12px; padding: 12px; color: #1e3a8a; background: #eff6ff; font-size: 12px; }
.sidebar-note span { color: #64748b; }
.backdrop { display: none; }
@media (max-width: 900px) {
  .sidebar { transform: translateX(-102%); transition: transform .2s ease; box-shadow: 12px 0 28px rgba(15, 23, 42, .12); }
  .sidebar--open { transform: translateX(0); }
  .close { display: grid; place-items: center; }
  .backdrop { position: fixed; inset: 0; z-index: 35; display: block; background: rgba(15, 23, 42, .28); backdrop-filter: blur(2px); }
}
</style>

