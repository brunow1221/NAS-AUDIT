# Modelo de datos inicial

## `audit_events`

Evidencia técnica inmutable: timestamp UTC, cuenta/IP, operación, acción interpretada, certeza, resultado, recurso, rutas, mensaje original, origen, modo de datos, claves de deduplicación y versión de parser.

Índices iniciales: tiempo descendente, usuario+tiempo, acción+tiempo, recurso+tiempo, fuente y clave de deduplicación única. El diseño admite cientos de miles de filas; particionamiento se evaluará con volumen real.

## `event_sources`

Describe la procedencia (`CSV`, `Journal`, `Synthetic`) sin mezclarla con el evento.

## `import_jobs`

Conserva archivo, SHA-256, estado, conteos, duración y errores. El hash único evita reimportar silenciosamente la misma exportación.

## Identidad

Tablas ASP.NET Core Identity con claves UUID, roles `Administrator` y `Auditor`, bloqueo de intentos, estado habilitado, último acceso, último cambio de contraseña y obligación de cambiar claves temporales.

## Recolección en vivo

- `collector_checkpoints`: último cursor confirmado por agente, boot ID, fecha del evento y fuente.
- `collector_heartbeats`: salud, última observación, cola pendiente y metadatos operativos.
- `ingestion_batches`: resultado idempotente de cada lote recibido.
- `rejected_ingestion_events`: conserva el mensaje técnico original cuando el parser aún no reconoce su formato; su clave única evita duplicarlo y permite avanzar el cursor sin perder evidencia.

`maintenance_runs` conserva ejecuciones de retención y cambios administrativos relevantes con actor, estado, tiempos y detalle no secreto. Los grupos de actividad de Fase 4 son proyecciones temporales y no una tabla duplicada.

## Índices de Fase 5

Se añaden combinaciones `DataMode+EventTimestampUtc`, `DataMode+SmbUser+EventTimestampUtc`, `DataMode+InterpretedAction+EventTimestampUtc` y heartbeat por tiempo. Su objetivo es sostener las vistas operativas sin duplicar evidencia.

## Consultas de Fase 4

El explorador, perfiles SMB y grupos de actividad son proyecciones de `audit_events`; no duplican evidencia en tablas derivadas. Las rutas largas utilizan índices hash para comparaciones de igualdad en `FilePath`, `SourcePath` y `DestinationPath`. El análisis por extensión usa un índice compuesto con timestamp.

Las agrupaciones se calculan bajo demanda y conservan los identificadores de los eventos que las componen. No incrementan métricas ni sustituyen la evidencia original.

