#!/bin/sh
set -eu

echo "BCMETALS NAS Audit - diagnóstico journald (solo lectura)"
echo "Fecha UTC: $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
echo "Usuario: $(id)"

if ! command -v journalctl >/dev/null 2>&1; then
  echo "FAIL: journalctl no está disponible en el host."
  exit 1
fi

echo "journalctl: $(command -v journalctl)"
echo "Persistencia y permisos:"
for path in /run/log/journal /var/log/journal; do
  if [ -e "$path" ]; then
    stat -c '%n uid=%u gid=%g mode=%a' "$path"
  else
    echo "$path: ausente"
  fi
done

echo "Uso del journal:"
journalctl --disk-usage --no-pager 2>&1 || true

sample_file="$(mktemp)"
trap 'rm -f "$sample_file"' EXIT HUP INT TERM
if ! journalctl --output=json --no-pager -n 500 >"$sample_file" 2>/dev/null; then
  echo "FAIL: el usuario actual no puede leer journald."
  exit 2
fi

echo "Campos disponibles en la muestra (sin mostrar mensajes):"
for field in MESSAGE __CURSOR __REALTIME_TIMESTAMP _BOOT_ID SYSLOG_IDENTIFIER _COMM; do
  count="$(grep -c "\"$field\"" "$sample_file" || true)"
  echo "$field=$count"
done

audit_count="$(grep -c 'ugreen_syslog|' "$sample_file" || true)"
echo "Eventos con marcador ugreen_syslog en últimas 500 entradas: $audit_count"
echo "Identificadores candidatos (_COMM/SYSLOG_IDENTIFIER):"
sed -n 's/.*"_COMM":"\([^"]*\)".*/_COMM=\1/p; s/.*"SYSLOG_IDENTIFIER":"\([^"]*\)".*/SYSLOG_IDENTIFIER=\1/p' "$sample_file" | sort -u | head -50

echo "Grupos que podrían otorgar lectura:"
for path in /run/log/journal /var/log/journal; do
  if [ -e "$path" ]; then
    gid="$(stat -c '%g' "$path")"
    group="$(getent group "$gid" 2>/dev/null | cut -d: -f1 || true)"
    echo "$path gid=$gid group=${group:-desconocido}"
  fi
done

if command -v docker >/dev/null 2>&1; then
  echo "Docker disponible: sí"
  docker version --format 'Servidor={{.Server.Version}}' 2>/dev/null || echo "Docker existe, pero este usuario no accede al daemon."
else
  echo "Docker disponible: no"
fi

echo "Diagnóstico terminado. No se modificó journald, Samba ni ningún archivo empresarial."
