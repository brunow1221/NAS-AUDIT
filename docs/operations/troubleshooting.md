# Solución de problemas

- **La web no abre:** revise `docker compose ps`, el firewall y que `APP_PORT` no esté ocupado.
- **API no saludable:** `docker compose logs --tail=200 nas-audit-api`; confirme secretos y salud de PostgreSQL.
- **No puedo iniciar sesión:** la cuenta inicial solo se crea si no existe; revise email, política de 14 caracteres y reloj del NAS.
- **Importación rechazada:** confirme extensión `.csv`, tamaño menor a 20 MB y columnas/fechas reconocibles.
- **Dashboard vacío:** es correcto hasta importar históricos; demo está separado y desactivado por defecto.
- **“Historial solamente”:** el frontend desplegado está desactualizado o el recolector no está activo. Revise versión, perfil `collector` y heartbeat en Configuración.
- **Cambio de contraseña requerido:** es intencional para cuentas iniciales o restablecidas. Abra Configuración y cambie la contraseña antes de usar el resto del portal.
- **Retención desactivada:** es el estado seguro predeterminado; no active la variable sin aprobación, vista previa y respaldo verificado.
- **Actualización fallida:** use el identificador UTC informado por `scripts/update.sh` con `scripts/rollback.sh`; no restaure la base salvo evaluación explícita.

