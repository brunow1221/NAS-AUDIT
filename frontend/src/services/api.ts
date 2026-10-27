import type { ApiEnvelope } from '../types/api'

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
  }
}

let csrfToken: string | null = null

async function responseMessage(response: Response): Promise<string> {
  try {
    const body = await response.json() as { title?: string; detail?: string }
    return body.detail ?? body.title ?? `Error HTTP ${response.status}`
  } catch {
    return `Error HTTP ${response.status}`
  }
}

export async function ensureCsrf(): Promise<string> {
  if (csrfToken) return csrfToken
  const response = await fetch('/api/v1/auth/csrf', { credentials: 'include' })
  if (!response.ok) throw new ApiError(response.status, await responseMessage(response))
  const payload = await response.json() as ApiEnvelope<{ token: string }>
  csrfToken = payload.data.token
  return csrfToken
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-XSRF-TOKEN', await ensureCsrf())
  }
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')

  const response = await fetch(path, { ...options, headers, credentials: 'include' })
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new CustomEvent('auth:unauthorized'))
    throw new ApiError(response.status, await responseMessage(response))
  }
  const payload = await response.json() as ApiEnvelope<T>
  return payload.data
}

export async function downloadFile(path: string): Promise<void> {
  const response = await fetch(path, { credentials: 'include' })
  if (!response.ok) throw new ApiError(response.status, await responseMessage(response))
  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  const plainName = disposition.match(/filename="?([^";]+)"?/i)?.[1]
  const fileName = encodedName ? decodeURIComponent(encodedName) : plainName ?? 'bcmetals-auditoria.csv'
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function resetCsrf(): void {
  csrfToken = null
}

