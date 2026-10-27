#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 2 || "$1" != "--confirm-restore" ]]; then
  echo "Uso: sudo ./scripts/restore-backup.sh --confirm-restore backups/archivo.dump" >&2
  echo "La restauración reemplaza la base activa. Verifique primero con verify-backup.sh." >&2
  exit 2
fi
root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
backup="$2"
test -f "$backup" || { echo "ERROR: no existe $backup" >&2; exit 1; }
./scripts/verify-backup.sh "$backup"
./scripts/backup.sh

echo "Deteniendo API, web y recolector; PostgreSQL permanecerá activo."
docker compose --profile collector stop nas-audit-web nas-audit-collector nas-audit-api
docker compose exec -T nas-audit-db sh -c \
  'dropdb --force --if-exists -U "$POSTGRES_USER" "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" -O "$POSTGRES_USER" "$POSTGRES_DB"'
docker compose exec -T nas-audit-db sh -c \
  'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --exit-on-error --no-owner --no-privileges' < "$backup"
docker compose --profile collector up -d --wait --wait-timeout 240
docker compose --profile collector ps
echo "Restauración completada desde $backup"
