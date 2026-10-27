#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^[0-9]{8}T[0-9]{6}Z$ ]]; then
  echo "Uso: sudo ./scripts/rollback.sh AAAAMMDDTHHMMSSZ" >&2
  exit 2
fi
stamp="$1"
root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
test -f "backups/releases/$stamp.manifest" || { echo "ERROR: punto de reversión inexistente" >&2; exit 1; }

for image in api web collector; do
  docker image inspect "bcmetals-nas-audit-rollback/$image:$stamp" >/dev/null
done

temporary_dir="$(mktemp -d -t bcmetals-rollback.XXXXXX)"
case "$temporary_dir" in /tmp/bcmetals-rollback.*) ;; *) echo "ERROR: directorio temporal inesperado" >&2; exit 1 ;; esac
cleanup() { rm -rf -- "$temporary_dir"; }
trap cleanup EXIT
override="$temporary_dir/rollback.yml"
cat > "$override" <<EOF
services:
  nas-audit-api:
    image: bcmetals-nas-audit-rollback/api:$stamp
  nas-audit-web:
    image: bcmetals-nas-audit-rollback/web:$stamp
  nas-audit-collector:
    image: bcmetals-nas-audit-rollback/collector:$stamp
EOF

docker compose -f docker-compose.yml -f "$override" --profile collector \
  up -d --no-build --force-recreate --wait --wait-timeout 240
docker compose --profile collector ps
echo "PASS: imágenes revertidas a $stamp. La base no fue reemplazada."
echo "Para una reversión de datos use restore-backup.sh sólo después de validar el dump."
