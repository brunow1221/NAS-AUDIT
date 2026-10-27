# Runbook de producción

Este procedimiento opera exclusivamente el proyecto `bcmetals-nas-audit`. No modifica Samba, los archivos empresariales ni contenedores ajenos.

## Comprobación diaria

```bash
cd /volume1/docker/bcmetals-nas-audit
sudo docker compose --profile collector ps
sudo docker compose logs --no-color --since=24h nas-audit-api nas-audit-collector | tail -200
loopback_address="$(getent ahostsv4 localhost | awk 'NR == 1 { print $1 }')"
curl --fail --silent --noproxy '*' --resolve "BCMETALSNAS:8443:${loopback_address}" \
  --cacert data/tls/ca.crt https://BCMETALSNAS:8443/healthz
```

En **Configuración** deben verse API, PostgreSQL y recolector operativos. Una observación es un umbral técnico para revisión humana; no demuestra actividad maliciosa.

## Respaldo verificable

```bash
sudo ./scripts/backup.sh
sudo ./scripts/verify-backup.sh backups/nas-audit-AAAAMMDDTHHMMSSZ.dump
```

El primer comando crea un dump personalizado, checksum SHA-256 y manifiesto. El segundo restaura el dump en PostgreSQL temporal, sin red y sin tocar la base activa. Copie periódicamente los tres archivos a otra ubicación protegida y respalde también `data/protection-keys`.

## Restauración

La restauración reemplaza la base activa. Requiere una ventana aprobada, dump verificado y confirmación literal:

```bash
sudo ./scripts/restore-backup.sh --confirm-restore backups/nas-audit-AAAAMMDDTHHMMSSZ.dump
```

El script verifica el dump, crea un respaldo previo, detiene API/web/recolector, restaura y exige que todos los servicios vuelvan saludables. No restaure un dump de origen desconocido.

## Actualización y reversión

```bash
sudo ./scripts/update.sh
```

La actualización ejecuta preflight, respaldo, guarda las imágenes activas con un identificador UTC, construye y espera salud. Si falla, utilice el identificador mostrado:

```bash
sudo ./scripts/rollback.sh AAAAMMDDTHHMMSSZ
```

La reversión recupera imágenes, no datos. Si una futura migración no fuese compatible hacia atrás, restaure datos sólo mediante el procedimiento anterior y después de una evaluación explícita.

## Reinicio controlado

```bash
sudo ./scripts/test-restart.sh
```

Comprueba salud, que el total de eventos no disminuya y que no se pierda el checkpoint del recolector.

## Retención

`RETENTION_ENABLED=false` es el estado inicial obligatorio. Antes de habilitarla, TI debe aprobar por escrito los plazos, ejecutar la vista previa en **Configuración**, generar y verificar un respaldo y definir responsable/ventana. La eliminación es irreversible.

## Incidentes

1. No borre contenedores ni datos durante el diagnóstico.
2. Capture `docker compose ps`, logs y hora UTC.
3. Si PostgreSQL está saludable pero la web no responde, revise primero Nginx y la publicación de `APP_PORT`.
4. Si el recolector falla, la consulta histórica continúa disponible; no modifique journald ni Samba.
5. Nunca use `docker compose down -v`, `docker system prune` o eliminación de `data/` como solución.
