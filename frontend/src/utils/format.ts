const locale = 'es-CL'
const zone = 'America/Santiago'

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(locale).format(value)
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return 'Sin registros'
  return new Intl.DateTimeFormat(locale, {
    timeZone: zone,
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function formatTime(value: string): string {
  return new Intl.DateTimeFormat(locale, { timeZone: zone, hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

export function formatDate(value: string): string {
  return new Intl.DateTimeFormat(locale, { timeZone: zone, day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value))
}

export const actionLabels: Record<string, string> = {
  Unknown: 'Desconocido',
  Creation: 'Creación',
  Open: 'Apertura',
  Write: 'Modificación',
  Deletion: 'Eliminación',
  Rename: 'Renombrado',
  Move: 'Movimiento',
  Connection: 'Conexión',
  ObservedRead: 'Lectura observada',
}

export function fileName(path: string | null): string {
  if (!path) return '—'
  return path.replaceAll('\\', '/').split('/').filter(Boolean).at(-1) ?? path
}

