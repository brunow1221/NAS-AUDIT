# Línea base de rendimiento

La optimización inicial añade índices compuestos para modo+fecha, usuario+fecha, acción+fecha y heartbeat, timeout SQL de 30 segundos, reintentos transitorios y límites de concurrencia para consultas analíticas.

## Medición controlada

Use una cuenta **Auditor** temporal, no la cuenta administradora. Ejecute desde un equipo de la misma LAN:

```powershell
$env:BASE_URL='https://BCMETALSNAS:8443'
$env:NODE_EXTRA_CA_CERTS='C:\ruta\bcmetals-nas-audit-ca.crt'
$env:TEST_EMAIL='carga@bcmetals.local'
$env:TEST_PASSWORD='CONTRASEÑA_TEMPORAL'
$env:DURATION_SECONDS='30'
$env:CONCURRENCY='8'
node .\scripts\load-test.mjs
Remove-Item Env:TEST_PASSWORD
Remove-Item Env:NODE_EXTRA_CA_CERTS
```

El ensayo consulta estado, dashboard, eventos, usuarios SMB y estadísticas. Falla si supera 1% de errores o p95 de 2 segundos. No incremente concurrencia sobre 50 ni duración sobre 600 segundos.

## Resultado de aceptación

Registre fecha, versión, total de eventos, hardware, RPS, p50/p95/p99, errores, CPU y memoria. La aceptación final debe realizarse sobre el NAS porque una prueba local no representa su almacenamiento ni su motor Docker.

Los límites iniciales del Compose son conservadores. Sólo deben cambiarse después de dos mediciones comparables y confirmando que PostgreSQL no entra en presión de memoria.

## Línea base aceptada — NAS UGREEN

Medición ejecutada el 27 de octubre de 2026 contra Nginx por loopback en el NAS, después de aplicar `Phase5Operations` y validar un reinicio completo.

| Parámetro | Resultado |
| --- | ---: |
| Arquitectura / memoria | x86_64 / 7,5 GiB |
| Docker / Compose | 29.6.2 / 5.1.3 |
| Duración / concurrencia | 30 s / 8 |
| Solicitudes | 9.405 |
| Solicitudes por segundo | 313,5 |
| Errores | 0 (0%) |
| p50 / p95 / p99 | 20,7 / 62,7 / 92,6 ms |

El criterio de aceptación fue satisfecho: errores menores o iguales a 1% y p95 menor o igual a 2.000 ms. Esta medición establece capacidad de servidor por loopback; una medición desde la LAN incluirá además latencia de red y navegador.
