#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
test -f .env || { echo "FAIL: falta .env" >&2; exit 1; }

echo "BCMETALS NAS Audit - preflight de producción"
echo "fecha_utc=$(date -u +%FT%TZ) arquitectura=$(uname -m)"
docker version --format 'docker_server={{.Server.Version}}'
docker compose version
docker compose --profile collector config -q

if grep -Eq 'CHANGE_ME|not-configured' .env; then
  echo "FAIL: .env aún contiene marcadores inseguros" >&2
  exit 1
fi

read_env_value() {
  local key="$1"
  sed -n "s/^${key}=//p" .env | tail -n 1 | tr -d '\r'
}

app_port="$(read_env_value APP_PORT)"
app_port="${app_port:-8092}"
if [[ ! "$app_port" =~ ^[0-9]{1,5}$ ]] || (( app_port < 1 || app_port > 65535 )); then
  echo "FAIL: APP_PORT debe ser un puerto numérico válido" >&2
  exit 1
fi
https_port="$(read_env_value APP_HTTPS_PORT)"
https_port="${https_port:-8443}"
if [[ "$https_port" != "8443" ]]; then
  echo "FAIL: APP_HTTPS_PORT debe ser 8443 porque la redirección segura usa ese puerto" >&2
  exit 1
fi
journal_path="$(read_env_value JOURNAL_PATH)"
journal_path="${journal_path:-/run/log/journal}"
case "$journal_path" in
  /run/log/journal|/var/log/journal) ;;
  *) echo "FAIL: JOURNAL_PATH no corresponde a una ruta journald permitida" >&2; exit 1 ;;
esac

printf 'puerto_publicado=%s\n' "$app_port"
printf 'puerto_https=%s\n' "$https_port"
df -h "$root_dir"
free -h || true
stat -c '%n uid=%u gid=%g mode=%a' .env data data/postgres data/protection-keys data/collector data/tls
test -r "$journal_path" || { echo "FAIL: journal no legible" >&2; exit 1; }
for tls_file in ca.crt server.crt server-fullchain.crt server.key endpoint.conf; do
  test -r "data/tls/$tls_file" || { echo "FAIL: falta data/tls/$tls_file" >&2; exit 1; }
done
openssl verify -CAfile data/tls/ca.crt data/tls/server.crt >/dev/null
openssl x509 -checkend 2592000 -noout -in data/tls/server.crt >/dev/null || { echo "FAIL: el certificado TLS vence en menos de 30 días" >&2; exit 1; }
docker compose --profile collector ps
echo "PASS: configuración válida; no se modificó ningún servicio."
