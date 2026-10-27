# Changelog

## 1.0.0 — 2026-10-27

- Cierra las Fases 0–5 como primera versión estable validada en el NAS UGREEN.
- Añade HTTPS interno con TLS 1.2/1.3, CA privada, redirección permanente, HSTS y cookies `Secure`.
- Documenta instalación de confianza por usuario y conserva claves/certificados fuera de Git.

## 0.5.0 — 2026-10-27

- Añade centro de operación, salud consolidada y observaciones explicables.
- Añade administración de cuentas locales, deshabilitación sin borrado y cambio obligatorio de contraseñas temporales.
- Añade política de retención con vista previa, lotes y ejecución desactivada por defecto.
- Añade respaldos con checksum/manifiesto, verificación aislada y restauración con confirmación y copia previa.
- Añade preflight, actualización con punto de reversión, rollback de imágenes y prueba de reinicio.
- Endurece proxies, cabeceras, límites de solicitud, capacidades, recursos y consultas.
- Añade prueba de carga acotada, runbook, revisión de seguridad y contrato operativo.

## 0.4.0 — Fase 4

- Explorador de carpetas deducidas de la evidencia, búsqueda paginada y línea de tiempo exacta para rutas principales, originales y destino.
- Perfiles de cuentas SMB por período con IP, recursos, acciones, actividad diaria y eventos recientes sin equiparar cuentas técnicas con personas.
- Estadísticas por acción, extensión, resultado y carpeta; agrupaciones explicativas por cuenta, IP, ruta y ventana temporal.
- Informes CSV descargables de eventos, cuentas, rutas y grupos, con límites y neutralización de fórmulas.
- Índices hash para igualdad de rutas largas e índice compuesto de extensión/fecha.
- Contrato preparado para candidatos de alerta; no se aplican umbrales, etiquetas maliciosas ni notificaciones en esta fase.
- En instalaciones con recolector activo, la primera visita prioriza Journald en vivo y distingue claramente ese origen del CSV histórico.

## 0.3.0 — Fase 3

- Agente Go 1.27 con lectura estructurada de journald, filtro de fuente, cola acotada, lotes y reintentos exponenciales.
- Token independiente del acceso web, API de ingesta idempotente y persistencia de batches/checkpoints/heartbeat; los formatos rechazados conservan su evidencia original.
- Cursor local atómico, recuperación tras reinicios y detección explícita de rotación con registro de posible brecha.
- Monitor SSE con reconexión, deduplicación, pausa visual, filtros y estado real del recolector.
- Despliegue Docker opt-in sin privilegios y alternativa systemd preparada para hosts incompatibles.
- Diagnóstico de journald de solo lectura y procedimiento de validación sobre `PRUEBA_AUDITORIA_SMB`.

## 0.2.0 — Fase 2

- Parser UGOS versionado con gramática por operación y clasificación de movimiento, lectura observada, papelera y flujos auxiliares.
- Vista previa, detección de estructura/codificación y carga secuencial de múltiples CSV.
- Deduplicación canónica entre archivos históricos solapados y contadores de repetidos.
- Historial con filtros completos, ordenamiento, búsqueda de evidencia, detalle enriquecido y paginación de servidor.
- Exportación CSV filtrada con límite operativo y neutralización de fórmulas.
- Pruebas para interpretación, importación solapada, vista previa y exportación segura.

## 0.1.0 - 2026-10-26

- Arquitectura y documentación de Fase 0.
- Aplicación web, API, persistencia, autenticación e importación mínima de Fase 1.
- Contratos preparados para el recolector de Fase 3, sin instalarlo.

