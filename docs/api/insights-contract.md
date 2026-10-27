# Contrato de exploración e informes — Fase 4

Todos los endpoints requieren una sesión web autenticada y devuelven un `ApiEnvelope<T>`, excepto las descargas CSV.

## Explorador

- `GET /api/v1/paths?mode=&search=&share=&page=&pageSize=` devuelve rutas agregadas y paginadas.
- `GET /api/v1/paths/history?path=&mode=` devuelve hasta 250 eventos recientes que mencionan la ruta como ruta principal, original o destino.
- `GET /api/v1/folders/history?path=&from=&to=&mode=` devuelve métricas, acciones, cuentas, subcarpetas deducidas y 100 eventos recientes para una carpeta y sus descendientes.

La jerarquía se construye exclusivamente con rutas observadas y no representa un inventario completo del NAS. La detección de subcarpetas se limita a 50.000 ocurrencias y comunica `pathSampleTruncated`; los totales de la carpeta se calculan sobre todo el período. Ninguna consulta abre ni inspecciona el documento original.

## Cuentas SMB

- `GET /api/v1/smb-users?mode=&search=&share=&from=&to=&page=&pageSize=` devuelve resúmenes por cuenta dentro del período.
- `GET /api/v1/smb-users/{user}/activity?from=&to=&mode=` devuelve acciones, IP, recursos, actividad diaria y 100 eventos recientes.

Los nombres se presentan como cuentas técnicas observadas y no como identidades personales verificadas.

## Estadísticas y grupos

- `GET /api/v1/statistics?from=&to=&mode=` devuelve tendencia diaria, extensiones, resultados y carpetas.
- `GET /api/v1/activity-groups?from=&to=&mode=&limit=` agrupa eventos consecutivos con igual cuenta, IP, recurso y ruta dentro de dos minutos.

La agrupación aporta contexto de navegación. No es una detección de amenaza ni una conclusión disciplinaria. Para evitar consumo no acotado, el análisis de carpetas utiliza como máximo 50.000 eventos recientes e informa `folderAnalysisTruncated`; la agrupación utiliza 5.000.

## Informes

`GET /api/v1/reports/{kind}?from=&to=&mode=` admite:

- `Events`
- `Users`
- `Paths`
- `ActivityGroups`

Las descargas son CSV UTF-8 con BOM, fechas UTC y un máximo de 100.000 filas. Los campos que comienzan como fórmulas de hoja de cálculo se neutralizan. La cabecera `X-Report-Rows` informa el número de filas.

## Preparación de alertas

`AlertCandidate` e `IAlertCandidateEvaluator` fijan el límite arquitectónico para Fase 5. No existe implementación activa, persistencia de reglas ni envío de notificaciones en Fase 4.
