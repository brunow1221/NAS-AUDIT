# Plan de implementación por fases

## Fase 0 — completada

Arquitectura, dependencias, modelo inicial, contratos, estrategia Docker, seguridad, riesgos y despliegue documentados.

## Fase 1 — completada y desplegada

Vue, API .NET, Identity, PostgreSQL/EF Core, migración inicial, Dockerfiles, Compose, dashboard, historial e importación mínima, validados en el NAS.

## Fase 2 — completada y desplegada

Parser versionado con gramática por operación, detección de CSV, vista previa y lotes múltiples, deduplicación canónica entre exportaciones, historial avanzado, detalle y exportación segura. La validación final con muestras reales anonimizadas debe continuar como tarea operativa porque UGOS puede variar sus encabezados entre versiones.

## Fase 3 — completada y desplegada

Recolector Go, autenticación independiente, checkpoint durable, ingesta idempotente, SSE y observabilidad implementados. El diagnóstico confirmó `/var/log/journal`, grupo `systemd-journal` y fuente primaria `smbd`; la prueba aislada en `PRUEBA_AUDITORIA_SMB` produjo eventos reales.

## Fase 4 — completada y desplegada

Explorador histórico navegable construido con rutas observadas, perfiles SMB por período, línea de tiempo, análisis por carpeta, estadísticas, agrupación contextual e informes CSV, validado sobre evidencia del NAS.

## Fase 5 — completada y desplegada

Índices y límites operativos, retención gobernada, respaldo/restauración verificable, candidatos explicables, administración de cuentas, endurecimiento, actualización/reversión, pruebas de reinicio y documentación final. El NAS validó respaldo/restauración aislada, migración, cuatro servicios saludables, conservación de eventos/checkpoint y una línea base de 313,5 solicitudes/s con 0% de errores y p95 de 62,7 ms. La retención y los candidatos conductuales permanecen desactivados hasta aprobación formal.

## Riesgos principales

| Riesgo | Mitigación |
| --- | --- |
| Exportaciones UGOS con columnas variables | conservar original, detectar estructura, rechazar fechas ambiguas y ampliar alias con muestras anonimizadas |
| Eventos técnicos múltiples por acción | conservar evidencia individual; las agrupaciones de Fase 4 son proyecciones explicativas que no alteran métricas |
| `create_file/open` confundido con descarga | clasificación explícita como apertura, nunca como lectura confirmada |
| Acceso del contenedor a journald | diagnóstico previo; systemd con usuario dedicado como alternativa |
| Crecimiento de la tabla | índices y paginación ahora; particionar solo tras medir |
| Pérdida por actualización | bind mount separado, respaldo previo y migraciones versionadas |

