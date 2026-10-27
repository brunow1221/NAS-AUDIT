# Arquitectura definitiva — Fases 0 a 5

## Decisiones

BCMETALS NAS Audit usa un monolito modular porque el dominio, la seguridad y las consultas comparten consistencia transaccional y no justifican microservicios. El recolector Go es el único proceso independiente, por su cercanía con journald.

| Capa | Responsabilidad |
| --- | --- |
| `frontend` | SPA, navegación, autenticación, filtros, gráficos, exploración e informes |
| `Domain` | Entidades, enums e invariantes sin infraestructura |
| `Application` | DTOs, contratos y casos de uso |
| `Infrastructure` | EF Core, Identity, PostgreSQL, importación y consultas |
| `Api` | HTTP, cookies, antiforgery, autorización, health checks y composición |

La API se publica solo detrás de Nginx. PostgreSQL vive en una red Compose interna sin puerto de host. El navegador nunca conoce credenciales de base ni secretos del futuro agente.

## Flujo histórico

1. Un administrador obtiene un token antiforgery e inicia sesión.
2. Carga un CSV por HTTPS/HTTP interno al endpoint de importaciones.
3. La API limita tamaño, calcula SHA-256, valida filas y conserva mensaje original.
4. EF Core guarda el trabajo y los eventos en PostgreSQL.
5. Dashboard e historial consultan únicamente el modo seleccionado (`Historical` o `Demo`).

## Evolución modular

- Fase 2 amplió el parser e importador sin cambiar las entidades principales ni invalidar los datos de Fase 1.
- Fase 3 agregó el módulo `collector/`, autenticación independiente, checkpoints durables, ingesta idempotente, salud y SSE usando el contrato versionado.
- Fase 4 agregó proyecciones de rutas y cuentas, agrupaciones calculadas, estadísticas e informes sin duplicar la evidencia.
- Fase 5 agregó operación de producción: retención opt-in, respaldos verificables, actualización con rollback, TLS interno y pruebas de reinicio/carga.

No se monta ninguna carpeta SMB en la API. El sistema no ofrece comandos arbitrarios ni operaciones sobre archivos empresariales.

