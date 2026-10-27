<script setup lang="ts">
import { LogOut, Menu, ShieldCheck } from '@lucide/vue'
import { useAuthStore } from '../../stores/auth'
import StatusPill from './StatusPill.vue'

defineProps<{ title: string }>()
defineEmits<{ menu: [] }>()
const auth = useAuthStore()

async function logout() {
  await auth.logout()
  window.location.assign('/login')
}
</script>

<template>
  <header class="topbar">
    <div class="title-wrap">
      <button class="menu" type="button" aria-label="Abrir menú" @click="$emit('menu')"><Menu :size="22" /></button>
      <div><p>Centro de auditoría</p><h1>{{ title }}</h1></div>
    </div>
    <div class="actions">
      <StatusPill label="Historial solamente" tone="warning" />
      <div class="account">
        <span class="avatar"><ShieldCheck :size="17" /></span>
        <div><strong>{{ auth.user?.displayName }}</strong><small>{{ auth.user?.email }}</small></div>
      </div>
      <button class="logout" type="button" title="Cerrar sesión" aria-label="Cerrar sesión" @click="logout"><LogOut :size="19" /></button>
    </div>
  </header>
</template>

<style scoped>
.topbar { display: flex; min-height: 76px; align-items: center; justify-content: space-between; gap: 18px; border-bottom: 1px solid #cfe3f7; padding: 12px 22px; background: rgba(255,255,255,.78); backdrop-filter: blur(12px); }
.title-wrap { display: flex; align-items: center; gap: 12px; }
.title-wrap p { margin: 0 0 2px; color: #64748b; font-size: 11px; font-weight: 700; letter-spacing: .07em; text-transform: uppercase; }
h1 { margin: 0; font-size: 21px; line-height: 1.2; }
.menu { display: none; border: 0; border-radius: 9px; padding: 8px; color: #1e3a8a; background: #eaf2ff; }
.actions, .account { display: flex; align-items: center; }
.actions { gap: 13px; }
.account { gap: 9px; padding-left: 2px; }
.avatar { display: grid; place-items: center; width: 35px; height: 35px; border-radius: 50%; color: #1d4ed8; background: #dbeafe; }
.account strong, .account small { display: block; max-width: 170px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.account strong { font-size: 13px; }.account small { margin-top: 1px; color: #64748b; font-size: 11px; }
.logout { display: grid; place-items: center; border: 0; border-radius: 9px; padding: 8px; color: #64748b; background: transparent; }
.logout:hover { color: #b91c1c; background: #fef2f2; }
@media (max-width: 900px) { .menu { display: grid; place-items: center; } }
@media (max-width: 680px) { .actions > :first-child, .account div { display: none; } .topbar { padding: 10px 14px; } h1 { font-size: 18px; } }
</style>

