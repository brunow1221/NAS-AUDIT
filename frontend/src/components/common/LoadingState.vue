<template>
  <div class="loading" role="status" aria-live="polite">
    <span class="spinner" aria-hidden="true" />
    <span>Cargando información…</span>
  </div>
</template>

<style scoped>
.loading { display: flex; min-height: 220px; align-items: center; justify-content: center; gap: 10px; color: #64748b; font-size: 14px; }
.spinner { width: 20px; height: 20px; border: 2px solid #bfdbfe; border-top-color: #1d4ed8; border-radius: 50%; animation: spin .7s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
</style>

