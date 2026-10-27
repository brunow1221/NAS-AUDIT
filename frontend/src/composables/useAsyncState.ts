import { ref } from 'vue'

export function useAsyncState() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function run<T>(operation: () => Promise<T>): Promise<T | undefined> {
    loading.value = true
    error.value = null
    try {
      return await operation()
    } catch (caught) {
      error.value = caught instanceof Error ? caught.message : 'Ocurrió un error inesperado.'
      return undefined
    } finally {
      loading.value = false
    }
  }

  return { loading, error, run }
}

