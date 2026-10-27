# Contrato operativo de Fase 5

Todos los endpoints usan el sobre JSON estándar y cookie de sesión con antiforgery para métodos mutables.

| Método y ruta | Rol | Propósito |
| --- | --- | --- |
| `GET /api/v1/system/status` | autenticado | salud consolidada, métricas, retención y observaciones |
| `POST /api/v1/auth/change-password` | autenticado | cambia la propia contraseña y levanta la restricción temporal |
| `GET /api/v1/admin/users` | Administrador | lista cuentas locales del portal |
| `POST /api/v1/admin/users` | Administrador | crea cuenta con contraseña temporal |
| `PUT /api/v1/admin/users/{id}/role` | Administrador | cambia entre Auditor y Administrator |
| `PUT /api/v1/admin/users/{id}/enabled` | Administrador | habilita o deshabilita sin borrar trazabilidad |
| `POST /api/v1/admin/users/{id}/reset-password` | Administrador | asigna contraseña temporal y exige cambio |
| `GET /api/v1/admin/retention/preview` | Administrador | cuenta filas elegibles sin borrar |
| `POST /api/v1/admin/retention/run` | Administrador | ejecuta política aprobada; falla si está desactivada |

`system/status` distingue salud operativa de candidatos conductuales. Cada candidato contiene clave de regla, severidad, ventana, conteo, explicación y dimensiones. Un candidato nunca constituye por sí mismo una conclusión de seguridad.
