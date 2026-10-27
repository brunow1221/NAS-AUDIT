# Dependencias fijadas

Selección verificada el 27 de octubre de 2026. Se usan versiones estables, no candidatas ni `latest`.

## Plataforma

| Componente | Versión |
| --- | --- |
| .NET SDK | 10.0.402 |
| ASP.NET Core Runtime / EF Core | 10.0.14 |
| Npgsql EF Core | 10.0.3 |
| PostgreSQL | 18.6 |
| CsvHelper | 33.1.0 |
| Serilog.AspNetCore | 10.0.0 |

.NET 10 es LTS hasta noviembre de 2028. Npgsql 10.0.3 es la línea estable compatible con EF Core 10. PostgreSQL 18 recibe soporte hasta noviembre de 2030.

## Frontend

| Paquete | Versión |
| --- | --- |
| Vue | 3.5.44 |
| Vue Router | 5.4.0 |
| Pinia | 4.1.0 |
| Vite | 8.3.5 |
| TypeScript | 6.0.3 |
| Tailwind CSS | 4.3.3 |
| Apache ECharts | 6.1.0 |
| @lucide/vue | 1.61.0 |
| Zod | 4.6.7 |
| Vitest | 5.0.5 |

TypeScript 6.0.3 se fija expresamente porque el verificador Vue vigente aún no puede resolver la ruta interna que cambió en TypeScript 7. El lockfile conserva el grafo exacto.

## Imágenes

- `postgres:18.6-alpine3.24`
- `mcr.microsoft.com/dotnet/sdk:10.0.402-alpine3.24`
- `mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24`
- `node:24-alpine3.24`
- `nginxinc/nginx-unprivileged:1.28-alpine3.23`

Los cinco tags se comprobaron en sus registros oficiales. Antes de una actualización, reconstruya en un entorno de prueba y respalde PostgreSQL; no cambie versiones mayores de base automáticamente.

