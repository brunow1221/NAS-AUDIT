import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { apiRequest, resetCsrf } from '../services/api'
import type { UserSession } from '../types/api'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserSession | null>(null)
  const initialized = ref(false)
  const loading = ref(false)

  const isAuthenticated = computed(() => user.value !== null)
  const isAdministrator = computed(() => user.value?.roles.includes('Administrator') ?? false)

  async function refresh(): Promise<void> {
    user.value = await apiRequest<UserSession>('/api/v1/auth/me')
    initialized.value = true
  }

  async function initialize(): Promise<void> {
    if (initialized.value) return
    try {
      await refresh()
    } catch {
      user.value = null
    } finally {
      initialized.value = true
    }
  }

  async function login(email: string, password: string): Promise<void> {
    loading.value = true
    try {
      user.value = await apiRequest<UserSession>('/api/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      resetCsrf()
      initialized.value = true
    } finally {
      loading.value = false
    }
  }

  async function logout(): Promise<void> {
    try {
      await apiRequest('/api/v1/auth/logout', { method: 'POST' })
    } finally {
      user.value = null
      resetCsrf()
    }
  }

  return { user, initialized, loading, isAuthenticated, isAdministrator, initialize, refresh, login, logout }
})

