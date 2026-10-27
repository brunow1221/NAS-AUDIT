#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
umask 077

if [[ ! -f .env ]]; then
  echo "ERROR: falta $root_dir/.env" >&2
  exit 1
fi

mkdir -p backups
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="${1:-backups/nas-audit-$stamp.dump}"
partial="$backup.partial"
manifest="$backup.manifest.txt"
trap 'rm -f -- "$partial"' EXIT

case "$backup" in
  backups/*.dump) ;;
  *) echo "ERROR: el destino debe ser backups/*.dump" >&2; exit 1 ;;
esac

docker compose exec -T nas-audit-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$partial"
test -s "$partial"
docker compose exec -T nas-audit-db pg_restore -l < "$partial" >/dev/null
mv -- "$partial" "$backup"

(
  cd -- "$(dirname -- "$backup")"
  sha256sum "$(basename -- "$backup")" > "$(basename -- "$backup").sha256"
)

event_count="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT count(*) FROM nas_audit.audit_events'\''')"
migration="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT "MigrationId" FROM nas_audit."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1'\''')"
{
  printf 'created_utc=%s\n' "$(date -u +%FT%TZ)"
  printf 'file=%s\n' "$backup"
  printf 'bytes=%s\n' "$(stat -c %s "$backup")"
  printf 'audit_events=%s\n' "$event_count"
  printf 'latest_migration=%s\n' "$migration"
  printf 'postgres_image=%s\n' "$(docker compose images nas-audit-db --format json | tr -d '\n')"
} > "$manifest"

echo "Respaldo creado y validado: $backup"
ls -lh -- "$backup" "$backup.sha256" "$manifest"
