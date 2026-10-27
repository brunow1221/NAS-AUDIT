<script setup lang="ts">
import { ref } from 'vue'
import { KeyRound, ShieldCheck } from '@lucide/vue'
import { apiRequest } from '../../services/api'
import { useAuthStore } from '../../stores/auth'

const emit = defineEmits<{ changed: [] }>()
const auth = useAuthStore()
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const message = ref('')
const submitting = ref(false)

async function submit() {
  message.value = ''
  if (newPassword.value !== confirmPassword.value) {
    message.value = 'La confirmación no coincide.'
    return
  }

  submitting.value = true
  try {
    await apiRequest('/api/v1/auth/change-password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword: currentPassword.value, newPassword: newPassword.value }),
    })
    await auth.refresh()
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
    message.value = 'Contraseña actualizada correctamente.'
    emit('changed')
  } catch (reason) {
    message.value = reason instanceof Error ? reason.message : 'No fue posible cambiar la contraseña.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <article class="panel security">
    <div class="card-title"><KeyRound :size="18" />Cambiar mi contraseña</div>
    <p>Use al menos 14 caracteres, mayúscula, minúscula, número y símbolo.</p>
    <form @submit.prevent="submit">
      <label class="label">Contraseña actual<input v-model="currentPassword" class="field" type="password" autocomplete="current-password" required /></label>
      <label class="label">Nueva contraseña<input v-model="newPassword" class="field" type="password" autocomplete="new-password" minlength="14" required /></label>
      <label class="label">Confirmar contraseña<input v-model="confirmPassword" class="field" type="password" autocomplete="new-password" minlength="14" required /></label>
      <span v-if="message" class="form-message">{{ message }}</span>
      <button class="btn-primary" :disabled="submitting" type="submit"><ShieldCheck :size="16" />{{ submitting ? 'Actualizando…' : 'Actualizar contraseña' }}</button>
    </form>
  </article>
</template>

<style scoped>
.security{padding:16px}.card-title{display:flex;align-items:center;gap:7px;color:#1e3a8a;font-size:13px;font-weight:800}.security>p{color:#64748b;font-size:10px}.security form{display:grid;gap:10px}.security label{display:grid;gap:4px}.form-message{color:#1d4ed8;font-size:10px}
</style>
