# Modelo de seguridad

- Cookie `HttpOnly`, `SameSite=Strict`, `Secure` obligatoria en producción y duración absoluta de 8 horas.
- Antiforgery de doble envío para login, logout e importaciones.
- Bloqueo temporal de Identity tras intentos fallidos y rate limit adicional por IP en login.
- Roles `Administrator` y `Auditor`; importar requiere `Administrator`.
- PostgreSQL no publica puerto y los contenedores usan `no-new-privileges` y sistemas de archivos de solo lectura donde es posible.
- No hay tokens de sesión en `localStorage`.
- Las claves de Data Protection persisten en `data/protection-keys`, separadas del código, para que las cookies sigan siendo verificables tras recrear el contenedor.
- Archivos limitados a `.csv` y 20 MB; no se ejecutan ni se guardan como archivos en el servidor.
- La exportación CSV neutraliza celdas que comienzan con `=`, `+`, `-`, `@`, tabulación o retorno de carro y limita cada consulta a 100.000 filas.
- La ingesta usa un token independiente de las sesiones, comparación en tiempo constante, límite de solicitudes y red interna. Nginx bloquea la ruta de ingesta pública.
- El recolector no usa root, modo privilegiado, red host, socket Docker ni montajes de escritura sobre journald.
- Las capacidades Linux de API, web y recolector se eliminan; los recursos tienen límites iniciales de CPU, memoria y procesos.
- Las cuentas temporales deben cambiar contraseña antes de usar las funciones del portal. Deshabilitar una cuenta conserva su trazabilidad.
- Los proxies reenviados se aceptan sólo desde la red Docker configurada; el cliente no puede imponer `X-Forwarded-For` a través de Nginx.
- Respaldos y restauraciones sólo se invocan desde SSH; la interfaz web no tiene acceso a Docker ni al sistema de archivos del NAS.

Nginx termina TLS 1.2/1.3 en el puerto 8443 usando la CA interna del proyecto. El puerto 8092 sólo responde healthcheck y redirige al portal seguro. Proteja `data/tls/ca.key`, distribuya únicamente `ca.crt` y restrinja ambos puertos a la LAN administrativa/Tailscale.

