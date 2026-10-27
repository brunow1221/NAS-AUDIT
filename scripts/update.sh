#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
./scripts/preflight.sh
./scripts/backup.sh

app_port="$(sed -n 's/^APP_PORT=//p' .env | tail -n 1 | tr -d '\r')"
app_port="${app_port:-8092}"
if [[ ! "$app_port" =~ ^[0-9]{1,5}$ ]] || (( app_port < 1 || app_port > 65535 )); then
  echo "ERROR: APP_PORT debe ser un puerto numérico válido" >&2
  exit 1
fi
https_port="$(sed -n 's/^APP_HTTPS_PORT=//p' .env | tail -n 1 | tr -d '\r')"
https_port="${https_port:-8443}"
tls_name="$(sed -n 's/^dns=//p' data/tls/endpoint.conf | tail -n 1 | tr -d '\r')"
loopback_address="$(getent ahostsv4 localhost | awk 'NR == 1 { print $1 }')"
if [[ "$https_port" != "8443" || -z "$tls_name" || -z "$loopback_address" ]]; then
  echo "ERROR: configuración HTTPS inválida" >&2
  exit 1
fi

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
release_dir="backups/releases"
mkdir -p "$release_dir"

for service in nas-audit-api nas-audit-web nas-audit-collector; do
  image_id="$(docker compose --profile collector images -q "$service" | head -n 1)"
  if [[ -z "$image_id" ]]; then
    echo "ERROR: no se encontró la imagen activa de $service" >&2
    exit 1
  fi
  short="${service#nas-audit-}"
  docker image tag "$image_id" "bcmetals-nas-audit-rollback/$short:$stamp"
done
printf 'stamp=%s\ncreated_utc=%s\n' "$stamp" "$(date -u +%FT%TZ)" > "$release_dir/$stamp.manifest"

echo "Punto de reversión de imágenes: $stamp"
docker compose --profile collector build
if ! docker compose --profile collector up -d --force-recreate --wait --wait-timeout 240; then
  echo "ERROR: la actualización no quedó saludable." >&2
  echo "Revierta las imágenes con: sudo ./scripts/rollback.sh $stamp" >&2
  exit 1
fi
docker compose --profile collector ps
curl --fail --silent --show-error "http://localhost:${app_port}/healthz" >/dev/null
curl --fail --silent --show-error --noproxy '*' \
  --resolve "${tls_name}:${https_port}:${loopback_address}" \
  --cacert data/tls/ca.crt \
  "https://${tls_name}:${https_port}/healthz" >/dev/null
echo "PASS: actualización saludable; rollback=$stamp"
