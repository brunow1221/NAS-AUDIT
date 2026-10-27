# Despliegue seguro en NAS UGREEN

Estas instrucciones no modifican Samba ni contenedores ajenos.

## 1. Diagnóstico de solo lectura

Conéctese por SSH y ejecute:

```bash
uname -m
docker --version
docker compose version
docker ps --format 'table {{.Names}}\t{{.Ports}}'
ss -ltn '( sport = :8092 )'
ss -ltn '( sport = :8443 )'
free -h
df -h /volume1
ls -ld /volume1/docker
```

Se espera `x86_64`/amd64. Los puertos 8092 y 8443 deben estar libres; no detenga servicios existentes para liberarlos. La redirección segura usa 8443 y no debe cambiarse sin actualizar también Nginx.

## 2. Preparar directorio

Transfiera este repositorio y, ya en el NAS:

```bash
mkdir -p /volume1/docker/bcmetals-nas-audit/data/postgres
mkdir -p /volume1/docker/bcmetals-nas-audit/data/protection-keys
mkdir -p /volume1/docker/bcmetals-nas-audit/data/collector
mkdir -p /volume1/docker/bcmetals-nas-audit/backups
cd /volume1/docker/bcmetals-nas-audit
cp .env.example .env
chmod 600 .env
sudo chown -R 70:70 data/postgres
sudo chmod 700 data/postgres
sudo chown -R 1654:1654 data/protection-keys
sudo chown -R 1655:1655 data/collector
sudo chmod 700 data/collector
```

Edite `.env` y reemplace todos los valores `CHANGE_ME`. Use una contraseña inicial de al menos 14 caracteres. Mantenga `DEMO_MODE=false` para datos reales.

Genere la CA y el certificado interno. El script no sobrescribe material existente:

```bash
sudo chmod 750 scripts/setup-internal-tls.sh
sudo ./scripts/setup-internal-tls.sh NAS_IP BCMETALSNAS
```

La clave `data/tls/ca.key` permanece sólo en el NAS. Distribuya únicamente `data/tls/ca.crt` a los equipos autorizados.

En cada perfil de Windows autorizado, copie la CA pública e impórtela en el almacén del usuario:

```powershell
$nasHost = 'BCMETALSNAS'
Copy-Item "\\$nasHost\docker\bcmetals-nas-audit\data\tls\ca.crt" "$env:TEMP\bcmetals-nas-audit-ca.crt"
Import-Certificate -FilePath "$env:TEMP\bcmetals-nas-audit-ca.crt" -CertStoreLocation 'Cert:\CurrentUser\Root'
```

Esta acción confía en certificados firmados por esta CA dentro de ese perfil. Verifique la huella antes de instalarla y nunca copie `ca.key`.

El UID 1654 corresponde al usuario no privilegiado de la imagen oficial ASP.NET usada en esta versión. Si se cambia la imagen base, verifique su UID antes de aplicar permisos. Respalde `data/protection-keys` junto con la base; perder esas claves invalida sesiones existentes, aunque no borra eventos.

El Dockerfile asigna la publicación `/app` al UID no privilegiado antes de arrancar. La validación `node scripts/validate-compose.mjs` también comprueba esta propiedad para evitar fallos de lectura de `appsettings.json` en NAS con permisos restrictivos.

PostgreSQL 18 Alpine usa `PGDATA=/var/lib/postgresql/18/docker` y el usuario interno `postgres` con UID/GID 70. Por ello el bind mount de `./data/postgres` debe apuntar al contenedor en `/var/lib/postgresql` y pertenecer a `70:70`. La validación `node scripts/validate-compose.mjs` protege la regla de montaje.

## 3. Validar y arrancar

```bash
cd /volume1/docker/bcmetals-nas-audit
docker compose config
docker compose build
docker compose up -d
docker compose ps
docker compose logs --tail=100 nas-audit-api
```

Instale `data/tls/ca.crt` como raíz confiable del usuario y abra `https://IP_DEL_NAS:8443`. `http://IP_DEL_NAS:8092` redirige al mismo portal mediante HTTPS.

En el despliegue Docker normal la API no publica ningún puerto del host; el recolector se comunica mediante `nas-audit-internal`. Esto evita colisiones y no depende del comportamiento de `host_ip` del motor del NAS.

El frontend pertenece a dos redes: `nas-audit-edge`, un bridge con `internal: false` para publicar el puerto web, y `nas-audit-internal` para comunicarse con la API. La API y PostgreSQL sólo pertenecen a la red interna. Un contenedor conectado únicamente a una red `internal: true` puede estar saludable y tener `HostConfig.PortBindings` configurado, pero carecer de publicación activa (`NetworkSettings.Ports` muestra `null`). Declarar `host_ip` no corrige esa topología.

Después de copiar una actualización de redes, aplíquela solo al frontend y compruebe el acceso desde el NAS:

```bash
sudo docker compose config --quiet
sudo docker compose up -d --no-deps --force-recreate --wait --wait-timeout 90 nas-audit-web
sudo docker compose port nas-audit-web 8443
loopback_address="$(getent ahostsv4 localhost | awk 'NR == 1 { print $1 }')"
curl --noproxy '*' --fail --show-error --max-time 10 \
  --resolve "BCMETALSNAS:8443:${loopback_address}" \
  --cacert data/tls/ca.crt \
  https://BCMETALSNAS:8443/healthz
```

Se espera el puerto HTTPS publicado, certificado válido y respuesta `ok`. Compruebe también `https://BCMETALSNAS:8443/` desde un equipo de la LAN; el healthcheck interno por sí solo no verifica la publicación del puerto.

## 4. Activar Fase 3

Primero transfiera y ejecute el diagnóstico sin modificar el host:

```bash
cd /volume1/docker/bcmetals-nas-audit
sh scripts/diagnose-journald.sh | tee /tmp/nas-audit-journal-diagnostic.txt
```

No continúe si `journalctl` no puede leerse. Tome del informe el GID de `/run/log/journal` o `/var/log/journal` y los identificadores que realmente correspondan al proceso Samba original. Luego edite `.env` y configure:

```text
COLLECTOR_TOKEN=<secreto aleatorio de 32 bytes o más>
COLLECTOR_ID=bcmetalsnas-primary
# Reservado para la alternativa systemd; no se usa con el recolector Docker.
COLLECTOR_API_PORT=<PUERTO_LOOPBACK_LIBRE_Y_VERIFICADO>
JOURNAL_GID=<GID diagnosticado>
JOURNAL_PATH=</run/log/journal o /var/log/journal, exactamente como exista en el diagnóstico>
COLLECTOR_SOURCE_IDENTIFIERS=smbd
```

Puede generar el token sin imprimirlo en pantalla:

```bash
umask 077
collector_secret="$(openssl rand -hex 32)"
sed -i "s|^COLLECTOR_TOKEN=.*$|COLLECTOR_TOKEN=$collector_secret|" .env
unset collector_secret
```

El archivo creado desde `.env.example` ya contiene esa clave; el comando la reemplaza sin imprimir el secreto. Active el perfil:

```bash
sudo install -d -o 1655 -g 1655 -m 700 data/collector
sudo docker compose --profile collector config --quiet
sudo docker compose --profile collector build nas-audit-api nas-audit-web nas-audit-collector
sudo docker compose --profile collector up -d --force-recreate --wait --wait-timeout 180
sudo docker compose --profile collector ps
sudo docker compose logs --no-color --tail=100 nas-audit-collector nas-audit-api
```

Compruebe que la ingesta no sea pública y que el estado sea visible después de iniciar sesión:

```bash
loopback_address="$(getent ahostsv4 localhost | awk 'NR == 1 { print $1 }')"
curl --fail --show-error --max-time 10 --noproxy '*' \
  --resolve "BCMETALSNAS:8443:${loopback_address}" --cacert data/tls/ca.crt \
  https://BCMETALSNAS:8443/healthz
test "$(curl --noproxy '*' --resolve "BCMETALSNAS:8443:${loopback_address}" --cacert data/tls/ca.crt -s -o /dev/null -w '%{http_code}' https://BCMETALSNAS:8443/api/v1/ingestion/events)" = "404"
```

La prueba funcional se limita a la carpeta autorizada:

```bash
test_file="/volume1/PRUEBA_AUDITORIA_SMB/fase3-$(date +%Y%m%d-%H%M%S).txt"
printf 'Prueba controlada Fase 3\n' > "$test_file"
printf 'Segunda escritura\n' >> "$test_file"
mv "$test_file" "${test_file%.txt}-renombrado.txt"
rm "${test_file%.txt}-renombrado.txt"
```

Abra **Tiempo real** y confirme los eventos. Estos comandos crean y eliminan exclusivamente un archivo de prueba; no los adapte a carpetas productivas.

## 5. Respaldo

Fase 5 incorpora respaldo con checksum, manifiesto y validación del formato:

```bash
cd /volume1/docker/bcmetals-nas-audit
sudo ./scripts/backup.sh
sudo ./scripts/verify-backup.sh backups/nas-audit-AAAAMMDDTHHMMSSZ.dump
```

La verificación restaura en un PostgreSQL temporal sin red y no modifica la base activa. Copie dump, `.sha256`, manifiesto y `data/protection-keys` a otra ubicación protegida. No incluya `.env` en Git.

## 6. Actualización y reversión

Para Fase 5 use el procedimiento transaccional de operación:

```bash
sudo ./scripts/preflight.sh
sudo ./scripts/update.sh
```

El script crea un respaldo, etiqueta las imágenes activas con un identificador UTC, reconstruye y exige salud. Si la actualización no queda saludable:

```bash
sudo ./scripts/rollback.sh AAAAMMDDTHHMMSSZ
```

Después del despliegue confirme la migración `Phase5Operations`, cambie la contraseña administrativa, deje vacíos `INITIAL_ADMIN_EMAIL` e `INITIAL_ADMIN_PASSWORD` y recree la API. Ejecute también `sudo ./scripts/test-restart.sh`.

La retención permanece con `RETENTION_ENABLED=false`. Consulte la vista previa en **Configuración** y el [runbook](../operations/production-runbook.md) antes de cualquier aprobación.

Compose recrea sólo los servicios de este proyecto y conserva `./data/postgres`. La reversión de imágenes no reemplaza la base. No use `down -v`, no borre `data/` y no ejecute `docker system prune` como parte del procedimiento.

## 7. Detener

```bash
docker compose down
```

Esto detiene la aplicación y elimina su red/contenedores; no borra los directorios persistentes. Para incluir el servicio opt-in use `docker compose --profile collector down`.

