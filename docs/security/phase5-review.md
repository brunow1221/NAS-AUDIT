# Revisión de seguridad de Fase 5

## Controles implementados

- API y PostgreSQL permanecen en redes internas; sólo Nginx publica el portal.
- Cookies HTTP-only, `SameSite=Strict`, antiforgery y `Secure` sobre HTTPS interno.
- Cabeceras de seguridad, cuerpo máximo de 25 MB, límites de cabeceras y rate limiting.
- Proxies confiables limitados a la red Docker configurada; Nginx reemplaza `X-Forwarded-For` con la IP observada.
- Contenedores de aplicación sin privilegios, con `no-new-privileges`, capacidades Linux eliminadas y sistema de archivos de sólo lectura donde corresponde.
- Cuentas locales administrables sin borrado: rol, habilitación y contraseña temporal. Siempre queda al menos un administrador habilitado.
- Contraseña temporal y administrador inicial obligan a cambiarla. Mientras esté pendiente, el backend bloquea las funciones del portal.
- Operaciones administrativas y de retención quedan registradas en `maintenance_runs`; nunca se registra la contraseña.
- Restauración sólo desde SSH, con confirmación literal, verificación aislada y respaldo previo.
- Retención y candidatos conductuales desactivados hasta aprobación y línea base reales.

## Decisiones deliberadas

- El portal no ejecuta comandos Docker, respaldos ni restauraciones.
- No se borran cuentas de aplicación para conservar trazabilidad; se deshabilitan.
- Las cuentas SMB observadas no se confunden con identidades personales ni con usuarios del portal.
- Los candidatos de alerta explican regla, ventana y conteo. No califican actividad como ataque.
- HTTP sólo conserva healthcheck y redirección permanente; el portal y la API se sirven por TLS 1.2/1.3 con `COOKIE_SECURE=true`.

## Riesgos residuales y tratamiento

| Riesgo | Tratamiento |
| --- | --- |
| Acceso físico o administrativo al NAS | controles de acceso del NAS, MFA donde esté disponible y revisión de administradores |
| Robo de `.env` o backups | permisos 600/700, almacenamiento fuera de Git y copia cifrada fuera del equipo |
| Cambio de formato UGOS | conservar evidencia rechazada y ampliar parser sólo con muestras verificadas |
| Umbrales sin línea base | candidatos desactivados; medir antes de habilitar |
| Fallo simultáneo NAS/disco | copia externa probada y simulacro periódico de restauración |
| CA privada interna | proteger `data/tls/ca.key`; distribuir únicamente `ca.crt` a los clientes autorizados |

## Cierre operativo

Después de desplegar, cambie la contraseña administrativa conocida, deje vacíos `INITIAL_ADMIN_EMAIL` e `INITIAL_ADMIN_PASSWORD` en `.env`, recree la API y confirme que el acceso continúa funcionando. No copie secretos a tickets, chats o logs.
