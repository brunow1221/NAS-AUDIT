import { readFileSync } from 'node:fs'

const files = ['docker-compose.yml', 'docker-compose.dev.yml']
let failed = false

for (const script of ['backup.sh', 'restore-backup.sh', 'preflight.sh', 'update.sh', 'setup-internal-tls.sh']) {
  const content = readFileSync(`scripts/${script}`, 'utf8')
  if (/^\s*(?:source|\.)\s+.*\.env\s*$/m.test(content)) {
    console.error(`scripts/${script}: .env es dato para Compose y nunca debe ejecutarse como código shell.`)
    failed = true
  }
}

for (const file of files) {
  const content = readFileSync(file, 'utf8')
  const usesPostgres18 = /image:\s*postgres:18(?:\.|-|\s)/.test(content)
  const usesLegacyMount = /:\/var\/lib\/postgresql\/data(?:\s|$)/m.test(content)
  const usesPostgres18Mount = /:\/var\/lib\/postgresql(?:\s|$)/m.test(content)

  if (usesPostgres18 && (!usesPostgres18Mount || usesLegacyMount)) {
    console.error(`${file}: PostgreSQL 18 debe montar el volumen en /var/lib/postgresql.`)
    failed = true
  }
}

if (failed) process.exit(1)

const apiDockerfile = readFileSync('backend/Dockerfile', 'utf8')
if (!/chown\s+-R\s+\$?\{?APP_UID\}?:?[^\s]*\s+\/app(?:\s|&&|$)/m.test(apiDockerfile)) {
  console.error('backend/Dockerfile: la publicación /app debe pertenecer al usuario ASP.NET no privilegiado.')
  process.exit(1)
}

const productionCompose = readFileSync('docker-compose.yml', 'utf8')
const systemdCompose = readFileSync('docker-compose.systemd.yml', 'utf8')
if (!/ConnectionStrings__Default:.*GSS Encryption Mode=Disable/m.test(productionCompose)) {
  console.error('docker-compose.yml: Npgsql debe desactivar GSS para evitar cargar Kerberos en la imagen Alpine.')
  process.exit(1)
}

if (!/nas-audit-collector:[\s\S]*profiles:\s*\["collector"\]/m.test(productionCompose)) {
  console.error('docker-compose.yml: el recolector debe permanecer opt-in hasta completar el diagnóstico journald.')
  process.exit(1)
}

if (/nas-audit-collector:[\s\S]*?(privileged:\s*true|network_mode:\s*host|docker\.sock)/m.test(productionCompose)) {
  console.error('docker-compose.yml: el recolector no puede usar privilegios, red host ni el socket Docker.')
  process.exit(1)
}

if (!/source:\s*\$\{JOURNAL_PATH[^\n]*\n\s*target:\s*\/run\/log\/journal\n\s*read_only:\s*true[\s\S]*?create_host_path:\s*false/m.test(productionCompose)) {
  console.error('docker-compose.yml: journald debe usar una ruta diagnosticada, solo lectura y sin crearla en el host.')
  process.exit(1)
}

if (/COLLECTOR_API_PORT|nas-audit-host/.test(productionCompose)) {
  console.error('docker-compose.yml: la opción Docker no debe publicar la API en el host.')
  process.exit(1)
}

if (!/Retention__Enabled:\s*\$\{RETENTION_ENABLED:-false\}/m.test(productionCompose)) {
  console.error('docker-compose.yml: la retención debe permanecer desactivada de forma predeterminada.')
  process.exit(1)
}

for (const service of ['nas-audit-api', 'nas-audit-web', 'nas-audit-collector']) {
  const block = productionCompose.match(new RegExp(`  ${service}:[\\s\\S]*?(?=\\n  nas-audit-|\\nnetworks:)`))?.[0] ?? ''
  if (!/cap_drop:\s*\n\s*- ALL/m.test(block) || !/no-new-privileges:true/m.test(block)) {
    console.error(`docker-compose.yml: ${service} debe eliminar capacidades y bloquear nuevos privilegios.`)
    process.exit(1)
  }
}

if (!/ForwardedHeaders__KnownNetwork:/m.test(productionCompose)) {
  console.error('docker-compose.yml: la cadena de proxy debe confiar sólo en la red Docker configurada.')
  process.exit(1)
}

if (!/App__CookieSecure:\s*\$\{COOKIE_SECURE:-true\}/m.test(productionCompose) ||
    !/target:\s*8443[\s\S]*?published:\s*"\$\{APP_HTTPS_PORT:-8443\}"/m.test(productionCompose) ||
    !/source:\s*\.\/data\/tls\/server-fullchain\.crt[\s\S]*?create_host_path:\s*false/m.test(productionCompose) ||
    !/source:\s*\.\/data\/tls\/server\.key[\s\S]*?create_host_path:\s*false/m.test(productionCompose)) {
  console.error('docker-compose.yml: HTTPS interno, cookies Secure y montajes TLS deben estar activos de forma segura.')
  process.exit(1)
}

if (!/published:\s*"\$\{COLLECTOR_API_PORT[^\n]*\n\s*host_ip:\s*"\$\{SYSTEMD_LOOPBACK_ADDRESS:/m.test(systemdCompose) ||
    !/nas-audit-host:\s*\n\s*name:\s*bcmetals-nas-audit-host\s*\n\s*driver:\s*bridge\s*\n\s*internal:\s*false/m.test(systemdCompose)) {
  console.error('docker-compose.systemd.yml: la API alternativa debe usar loopback y un bridge dedicado.')
  process.exit(1)
}

const nginx = readFileSync('frontend/nginx.conf', 'utf8')
if (/proxy_add_x_forwarded_for/.test(nginx)) {
  console.error('frontend/nginx.conf: X-Forwarded-For debe reemplazarse con la IP observada.')
  process.exit(1)
}
if (!/listen 8443 ssl;/m.test(nginx) ||
    !/ssl_protocols TLSv1\.2 TLSv1\.3;/m.test(nginx) ||
    !/Strict-Transport-Security/m.test(nginx) ||
    !/return 308 https:\/\/\$host:8443\$request_uri;/m.test(nginx)) {
  console.error('frontend/nginx.conf: falta terminación TLS, HSTS o redirección HTTPS.')
  process.exit(1)
}
if (!/location \^~ \/api\/v1\/ingestion\/\s*\{\s*return 404;/m.test(nginx)) {
  console.error('frontend/nginx.conf: Nginx debe bloquear el endpoint interno de ingesta.')
  process.exit(1)
}

const webDockerfile = readFileSync('frontend/Dockerfile', 'utf8')
if (!/COPY\s+--chown=nginx:nginx\s+frontend\/nginx\.conf\s+\/etc\/nginx\/conf\.d\/default\.conf/m.test(webDockerfile)) {
  console.error('frontend/Dockerfile: default.conf debe pertenecer al usuario nginx no privilegiado.')
  process.exit(1)
}

if (!/COPY\s+--from=build\s+--chown=nginx:nginx\s+\/app\/dist\s+\/usr\/share\/nginx\/html/m.test(webDockerfile)) {
  console.error('frontend/Dockerfile: los archivos estáticos deben pertenecer al usuario nginx no privilegiado.')
  process.exit(1)
}

console.log('Deployment configuration validation passed.')

