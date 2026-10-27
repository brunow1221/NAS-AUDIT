# Contrato del recolector — Fase 3

El contrato está activo cuando `Collector__Token` contiene un secreto de al menos 32 caracteres. Nginx bloquea `/api/v1/ingestion/*` y la API no publica ese acceso en la LAN; el agente Docker se comunica sólo por la red interna. La publicación opcional para systemd se define separadamente en `docker-compose.systemd.yml` y requiere comprobar tanto un puerto libre como el enlace real producido por el motor Docker.

`POST /api/v1/ingestion/events` recibirá lotes autenticados con credencial independiente de las sesiones web:

```json
{
  "collectorId": "bcmetalsnas-primary",
  "batchId": "uuid",
  "events": [
    {
      "cursor": "s=...",
      "bootId": "uuid",
      "timestampUtc": "2026-10-26T12:30:00Z",
      "message": "ugreen_syslog|soporte|client.example.invalid|unlinkat|ok|/volume1/PRUEBA_AUDITORIA_SMB/prueba.txt",
      "metadata": {}
    }
  ]
}
```

La aceptación es idempotente por recolector+boot+cursor y devuelve cantidades aceptadas/duplicadas/rechazadas. El cursor sólo avanza cuando cada entrada quedó guardada como evento interpretado o como evidencia rechazada con su mensaje original. Antes de activar el agente se ejecuta el diagnóstico de solo lectura en UGOS Pro.

Cabeceras obligatorias:

```text
X-Collector-Id: bcmetalsnas-primary
X-Collector-Token: <secreto de al menos 32 caracteres>
```

Respuesta:

```json
{
  "data": {
    "batchId": "uuid",
    "accepted": 1,
    "duplicates": 0,
    "rejected": 0,
    "confirmedCursor": "s=...",
    "replay": false
  }
}
```

`POST /api/v1/ingestion/heartbeat` utiliza las mismas cabeceras y estados `healthy`, `source_unavailable`, `recovering` o `error`. `GET /api/v1/events/stream` expone SSE únicamente a sesiones web autenticadas.

