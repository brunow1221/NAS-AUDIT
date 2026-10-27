<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppSidebar from '../components/common/AppSidebar.vue'
import AppTopbar from '../components/common/AppTopbar.vue'

const route = useRoute()
const menuOpen = ref(false)
const title = computed(() => String(route.meta.title ?? 'BCMETALS NAS Audit'))
</script>

<template>
  <div class="shell">
    <AppSidebar :open="menuOpen" @close="menuOpen = false" />
    <div class="main">
      <AppTopbar :title="title" @menu="menuOpen = true" />
      <main>
        <RouterView v-slot="{ Component }">
          <Transition name="page" mode="out-in"><component :is="Component" /></Transition>
        </RouterView>
      </main>
    </div>
  </div>
</template>

<style scoped>
.shell { min-height: 100vh; }
.main { min-height: 100vh; margin-left: 252px; }
main { padding: 20px; }
@media (max-width: 900px) { .main { margin-left: 0; } main { padding: 16px; } }
@media (max-width: 520px) { main { padding: 12px; } }
</style>

