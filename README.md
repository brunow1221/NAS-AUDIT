# BCMETALS NAS Audit

Plataforma privada para consultar, explorar y analizar actividad histórica y en vivo de archivos compartidos en el NAS UGREEN de BCMETALS. Esta entrega cubre las **Fases 0 a 5**: arquitectura, aplicación web, autenticación, procesamiento histórico, recolección journald, exploración, informes y operación continua.

> El sistema conserva evidencia de auditoría; no modifica archivos, permisos, cuentas ni la configuración Samba. Tampoco interpreta una apertura SMB como una descarga confirmada.

## Estado de esta entrega

- Aplicación Vue 3 responsive con dashboard, historial avanzado, detalle, filtros, búsqueda y exportación CSV.
- API ASP.NET Core 10 con autenticación por cookie HTTP-only, antiforgery, roles Administrador/Auditor y rate limiting de inicio de sesión.
- PostgreSQL con migraciones EF Core, índices y persistencia fuera de los contenedores.
- Datos históricos y demo separados mediante `dataMode`; demo está desactivado por defecto.
- Docker Compose de producción para `/volume1/docker/bcmetals-nas-audit` y Compose de desarrollo.
- Parser UGOS versionado por operación, vista previa/importación múltiple y deduplicación entre exportaciones solapadas.
- Recolector Go con cursores durables, reintentos, API autenticada, SSE y monitor en vivo.
- Explorador de rutas con línea de tiempo, perfiles SMB, estadísticas por carpeta y agrupación contextual de eventos.
- Informes CSV de evidencia, cuentas, rutas y grupos de actividad con controles de seguridad para hojas de cálculo.
- Centro operativo con salud consolidada, administración de cuentas, cambio obligatorio de contraseñas temporales y observaciones explicables.
- Respaldo verificable, restauración aislada/controlada, actualización con punto de reversión y prueba de reinicio.
- Retención por modo preparada pero desactivada hasta aprobación formal; candidatos conductuales desactivados hasta medir línea base.

Quedan fuera por diseño: notificaciones externas, respuesta automática, clasificación de conducta como maliciosa y activación de retención sin aprobación.

## Arquitectura

```text
Navegador HTTPS :8443
       |
       v
nas-audit-web (Nginx + Vue)
       | /api/*
       v
nas-audit-api (ASP.NET Core 10)
       |
       v
nas-audit-db (PostgreSQL 18)

NAS journald (solo lectura)
       |
       v
nas-audit-collector (Go, perfil opt-in)
       |
       v
nas-audit-api (red interna)
```

La base y la API no publican puertos. Nginx es el único servicio expuesto a la LAN y el recolector Docker se comunica por la red interna. La alternativa systemd dispone de `docker-compose.systemd.yml`, que sólo debe usarse después de verificar un puerto loopback libre y la compatibilidad del motor Docker. Consulte [arquitectura](docs/architecture/README.md), [dependencias](docs/architecture/dependencies.md), [modelo de datos](docs/architecture/data-model.md), [seguridad](docs/security/security-model.md) y [contrato del recolector](docs/api/collector-contract.md).

## Inicio rápido local

Requisitos: Docker Engine con Compose v2 y al menos 2 GB libres de RAM.

1. Copie `.env.example` a `.env`.
2. Reemplace todas las variables marcadas `CHANGE_ME` con secretos largos y únicos.
3. Genere la CA y el certificado internos para la dirección y nombre del servidor.
4. Valide y levante:

```bash
sudo ./scripts/setup-internal-tls.sh NAS_IP BCMETALSNAS
docker compose config
docker compose up -d --build
docker compose ps
```

Instale únicamente `data/tls/ca.crt` en el almacén de confianza del cliente y abra `https://BCMETALSNAS:8443`. El puerto HTTP redirige a HTTPS. Inicie sesión con `INITIAL_ADMIN_EMAIL` y `INITIAL_ADMIN_PASSWORD`; la contraseña sólo se usa al crear el primer administrador.

## Despliegue en UGREEN

Las instrucciones reproducibles y las comprobaciones previas están en [deployment/ugreen.md](docs/deployment/ugreen.md). El destino es:

```text
/volume1/docker/bcmetals-nas-audit
```

El procedimiento final está en el [runbook de producción](docs/operations/production-runbook.md). Antes de desplegar, ejecute la comprobación previa y conserve un respaldo verificable.

## Desarrollo sin Docker

Frontend:

```bash
cd frontend
npm ci
npm run dev
```

Backend (requiere SDK .NET 10 y PostgreSQL):

```bash
dotnet restore backend/BCMetals.NasAudit.slnx
dotnet test backend/BCMetals.NasAudit.slnx
dotnet run --project backend/src/BCMetals.NasAudit.Api
```

## Importación CSV histórica

Desde **Importar CSV**, seleccione uno o varios archivos de hasta 20 MB cada uno. Antes de importar se muestra el formato, codificación, delimitador y una vista previa validada. Se aceptan encabezados comunes en español o inglés, UTF-8/BOM y Windows-1252, coma, punto y coma o tabulación.

El parser aplica gramática explícita a operaciones `ugreen_syslog`, conserva la evidencia original y distingue creación, apertura, escritura, lectura observada, eliminación, renombrado, movimiento, conexión, flujos auxiliares y papelera. La deduplicación canónica por ocurrencia detecta la misma evidencia en exportaciones solapadas, conserva repeticiones legítimas dentro de un archivo y no mezcla CSV con la futura fuente journald.

El historial ofrece filtros por fecha, cuenta, IP, acción, operación, recurso, archivo, ruta, extensión, resultado, origen y certeza. La exportación respeta esos filtros, limita cada descarga a 100.000 filas y neutraliza valores que podrían convertirse en fórmulas de hoja de cálculo.

## Exploración e informes

- **Explorador:** jerarquía deducida únicamente de rutas observadas, análisis de carpeta y cronología de cada ruta, incluyendo ambos lados de un renombrado; no es un inventario del NAS.
- **Usuarios:** perfiles técnicos por período con IP, recursos, acciones, actividad diaria y evidencia reciente. Una cuenta no se presenta como identidad personal confirmada.
- **Estadísticas:** evolución por acción, extensiones, resultados, carpetas y grupos correlacionados en ventanas de dos minutos.
- **Informes:** CSV descargables por período y modo de datos para eventos, cuentas, rutas y grupos.

Las agrupaciones no constituyen alertas ni califican una conducta. Fase 4 sólo prepara un contrato de candidatos para que los umbrales se definan en Fase 5 después de medir una línea base real.

## Operación segura

```bash
sudo ./scripts/preflight.sh
sudo ./scripts/backup.sh
sudo ./scripts/update.sh
sudo ./scripts/test-restart.sh
```

Consulte el [runbook](docs/operations/production-runbook.md), la [revisión de seguridad](docs/security/phase5-review.md) y la [línea base de rendimiento](docs/operations/performance-baseline.md). No use `docker compose down -v`, `docker system prune` ni elimine `data/` en producción.

## Activación del recolector

La aplicación histórica sigue funcionando sin el agente. Antes de ejecutar `--profile collector`, complete el diagnóstico de solo lectura descrito en [collector/README.md](collector/README.md) y [deployment/ugreen.md](docs/deployment/ugreen.md). La primera prueba real se hace únicamente sobre `PRUEBA_AUDITORIA_SMB`.

## Pruebas

```bash
npm --prefix frontend ci
npm --prefix frontend run test
npm --prefix frontend run build
dotnet test backend/BCMetals.NasAudit.slnx
cd collector && go test ./... && go vet ./...
```

El workflow de GitHub Actions ejecuta estas comprobaciones sin desplegar en el NAS.

