import { ref, watch } from 'vue'
import { defineStore } from 'pinia'
import type { DataMode } from '../types/api'

export const usePreferencesStore = defineStore('preferences', () => {
  const storageKey = 'nas-audit-data-mode-v2'
  const saved = window.localStorage.getItem(storageKey)
  const legacy = window.localStorage.getItem('nas-audit-data-mode')
  const initial: DataMode = saved === 'Historical' || saved === 'Demo' || saved === 'Live'
    ? saved
    : legacy === 'Demo' ? 'Demo' : 'Live'
  const dataMode = ref<DataMode>(initial)
  watch(dataMode, value => window.localStorage.setItem(storageKey, value))
  return { dataMode }
})

