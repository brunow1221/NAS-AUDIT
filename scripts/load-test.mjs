import { performance } from 'node:perf_hooks'

const baseUrl = (process.env.BASE_URL ?? 'https://bcmetalsnas.local:8443').replace(/\/$/, '')
const email = process.env.TEST_EMAIL
const password = process.env.TEST_PASSWORD
const durationSeconds = Number.parseInt(process.env.DURATION_SECONDS ?? '30', 10)
const concurrency = Number.parseInt(process.env.CONCURRENCY ?? '8', 10)
const maxP95Ms = Number.parseInt(process.env.MAX_P95_MS ?? '2000', 10)
if (!email || !password) throw new Error('Configure TEST_EMAIL y TEST_PASSWORD sólo en el entorno de esta ejecución.')
if (durationSeconds < 5 || durationSeconds > 600 || concurrency < 1 || concurrency > 50) throw new Error('Parámetros de carga fuera de límites seguros.')

const cookieJar = new Map()
function absorbCookies(headers) {
  const values = typeof headers.getSetCookie === 'function' ? headers.getSetCookie() : [headers.get('set-cookie')].filter(Boolean)
  for (const value of values) {
    const pair = value.split(';', 1)[0]; const separator = pair.indexOf('=')
    if (separator > 0) cookieJar.set(pair.slice(0, separator), pair.slice(separator + 1))
  }
}
function cookies() { return [...cookieJar].map(([key, value]) => `${key}=${value}`).join('; ') }

const csrfResponse = await fetch(`${baseUrl}/api/v1/auth/csrf`)
absorbCookies(csrfResponse.headers)
if (!csrfResponse.ok) throw new Error(`CSRF HTTP ${csrfResponse.status}`)
const csrf = (await csrfResponse.json()).data.token
const loginResponse = await fetch(`${baseUrl}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'content-type': 'application/json', 'x-xsrf-token': csrf, cookie: cookies() },
  body: JSON.stringify({ email, password }),
})
absorbCookies(loginResponse.headers)
if (!loginResponse.ok) throw new Error(`Login HTTP ${loginResponse.status}`)

const endpoints = [
  '/api/v1/system/status',
  '/api/v1/dashboard/summary?mode=Live',
  '/api/v1/events?mode=Live&page=1&pageSize=25',
  '/api/v1/smb-users?mode=Live&page=1&pageSize=25',
  '/api/v1/statistics?mode=Live',
]
const samples = []
let failures = 0
let requests = 0
const deadline = performance.now() + durationSeconds * 1000

async function worker(offset) {
  let index = offset
  while (performance.now() < deadline) {
    const started = performance.now()
    try {
      const response = await fetch(`${baseUrl}${endpoints[index % endpoints.length]}`, { headers: { cookie: cookies() } })
      await response.arrayBuffer()
      if (!response.ok) failures++
    } catch { failures++ }
    samples.push(performance.now() - started); requests++; index++
  }
}
await Promise.all(Array.from({ length: concurrency }, (_, index) => worker(index)))
samples.sort((a, b) => a - b)
const percentile = value => samples[Math.min(samples.length - 1, Math.floor(samples.length * value))] ?? 0
const result = {
  baseUrl,
  durationSeconds,
  concurrency,
  requests,
  requestsPerSecond: Number((requests / durationSeconds).toFixed(2)),
  failures,
  failurePercent: Number((failures * 100 / Math.max(requests, 1)).toFixed(2)),
  p50Ms: Number(percentile(.50).toFixed(1)),
  p95Ms: Number(percentile(.95).toFixed(1)),
  p99Ms: Number(percentile(.99).toFixed(1)),
}
console.log(JSON.stringify(result, null, 2))
if (result.failurePercent > 1 || result.p95Ms > maxP95Ms) process.exitCode = 1
