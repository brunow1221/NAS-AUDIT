# Recolector Go — Fase 3

Agente de entrega al menos una vez para los eventos originales de auditoría SMB presentes en journald. Lee JSON estructurado con `journalctl`, filtra por `_COMM`/`SYSLOG_IDENTIFIER`, envía lotes autenticados a la API y sólo guarda el cursor local después de una confirmación durable.

## Seguridad

- Sin modo privilegiado, `network_mode: host`, montaje de `/` ni socket Docker.
- Journal montado en solo lectura y acceso mediante el GID diagnosticado.
- Contenedor sin root (UID/GID 1655), filesystem de solo lectura y checkpoint en un bind mount dedicado.
- Token independiente de las cookies web y endpoint de ingesta no publicado por Nginx.
- Cola acotada; si la API no responde, la lectura aplica contrapresión y el lote mantiene su `batchId` durante los reintentos.

## Diagnóstico obligatorio

En el NAS, antes de activar el perfil:

```bash
cd /volume1/docker/bcmetals-nas-audit
sh scripts/diagnose-journald.sh | tee /tmp/nas-audit-journal-diagnostic.txt
```

El script es de solo lectura y no imprime el contenido de `MESSAGE`. Use el resultado para definir `JOURNAL_PATH`, `JOURNAL_GID` y `COLLECTOR_SOURCE_IDENTIFIERS` en `.env`. Compose usa `create_host_path: false`: una ruta equivocada falla de forma segura y no crea directorios en el host.

## Docker (opción preferida)

```bash
sudo install -d -o 1655 -g 1655 -m 700 data/collector
sudo docker compose --profile collector build nas-audit-api nas-audit-web nas-audit-collector
sudo docker compose --profile collector up -d --force-recreate --wait --wait-timeout 180
```

El agente expone `/healthz` y `/metrics` sólo en su red Docker; no publica puerto al host.

## systemd (alternativa)

Use `deploy/systemd/` únicamente si el diagnóstico demuestra que el contenedor sin privilegios no puede leer journald. Esa alternativa requiere elegir primero un puerto libre y validar `docker-compose.systemd.yml`; no forma parte del despliegue Docker normal. En este NAS no debe asumirse que `host_ip` será respetado, por lo que su activación exige una comprobación adicional del enlace real. Crear usuario/grupo, copiar binario y habilitar el servicio cambia el host y requiere autorización operativa explícita.

## Recuperación

Al reiniciar, el agente retoma `checkpoint.json`. Si journald rotó y el cursor dejó de existir, registra `possibleGap=true`, limpia el cursor local y continúa desde el momento actual. Nunca inventa los eventos ausentes.
