#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 ]]; then
  echo "Uso: sudo ./scripts/verify-backup.sh backups/archivo.dump" >&2
  exit 2
fi
root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
backup="$1"
test -f "$backup" || { echo "ERROR: no existe $backup" >&2; exit 1; }

if [[ -f "$backup.sha256" ]]; then
  (cd -- "$(dirname -- "$backup")" && sha256sum -c "$(basename -- "$backup").sha256")
fi

container="bcmetals-nas-audit-restore-check-$$"
test_db="nas_audit_restore_check"
test_user="restore_check"
test_password="$(od -An -N24 -tx1 /dev/urandom | tr -d ' \n')"
cleanup() { docker rm -f "$container" >/dev/null 2>&1 || true; }
trap cleanup EXIT

docker run -d --name "$container" --network none \
  --tmpfs /var/lib/postgresql:rw,nosuid,size=1g \
  -e POSTGRES_DB="$test_db" \
  -e POSTGRES_USER="$test_user" \
  -e POSTGRES_PASSWORD="$test_password" \
  postgres:18.6-alpine3.24 >/dev/null

for _ in $(seq 1 60); do
  if docker exec "$container" pg_isready -U "$test_user" -d "$test_db" >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$container" pg_isready -U "$test_user" -d "$test_db" >/dev/null
docker cp "$backup" "$container:/tmp/backup.dump"
docker exec "$container" pg_restore -U "$test_user" -d "$test_db" \
  --exit-on-error --no-owner --no-privileges /tmp/backup.dump

event_count="$(docker exec "$container" psql -U "$test_user" -d "$test_db" -Atc 'SELECT count(*) FROM nas_audit.audit_events')"
migration_count="$(docker exec "$container" psql -U "$test_user" -d "$test_db" -Atc 'SELECT count(*) FROM nas_audit."__EFMigrationsHistory"')"
test "$migration_count" -gt 0
echo "PASS: restauración aislada correcta; eventos=$event_count migraciones=$migration_count"
