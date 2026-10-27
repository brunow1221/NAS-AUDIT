#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
before="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT count(*) FROM nas_audit.audit_events'\''')"
cursor_before="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT "Cursor" FROM nas_audit.collector_checkpoints ORDER BY "UpdatedAtUtc" DESC LIMIT 1'\''')"

docker compose --profile collector restart nas-audit-db nas-audit-api nas-audit-collector nas-audit-web
docker compose --profile collector up -d --wait --wait-timeout 240

after="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT count(*) FROM nas_audit.audit_events'\''')"
cursor_after="$(docker compose exec -T nas-audit-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc '\''SELECT "Cursor" FROM nas_audit.collector_checkpoints ORDER BY "UpdatedAtUtc" DESC LIMIT 1'\''')"

if (( after < before )); then
  echo "FAIL: el total de eventos disminuyó ($before -> $after)" >&2
  exit 1
fi
if [[ -n "$cursor_before" && -z "$cursor_after" ]]; then
  echo "FAIL: se perdió el checkpoint del recolector" >&2
  exit 1
fi
docker compose --profile collector ps
echo "PASS: reinicio correcto; eventos=$before->$after checkpoint=conservado"
