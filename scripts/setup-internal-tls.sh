#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Uso: sudo ./scripts/setup-internal-tls.sh IP_DEL_NAS [NOMBRE_DNS]" >&2
  exit 2
fi

nas_ip="$1"
nas_name="${2:-BCMETALSNAS}"
if [[ ! "$nas_ip" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
  echo "ERROR: la dirección IPv4 no es válida" >&2
  exit 1
fi
if [[ ! "$nas_name" =~ ^[A-Za-z0-9][A-Za-z0-9.-]*$ ]]; then
  echo "ERROR: el nombre DNS no es válido" >&2
  exit 1
fi
command -v openssl >/dev/null || { echo "ERROR: openssl no está instalado" >&2; exit 1; }

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$root_dir"
tls_dir="$root_dir/data/tls"
umask 077
mkdir -p "$tls_dir"

for file in ca.key ca.crt server.key server.crt server-fullchain.crt endpoint.conf; do
  if [[ -e "$tls_dir/$file" ]]; then
    echo "ERROR: $tls_dir/$file ya existe; no se sobrescribirá" >&2
    exit 1
  fi
done

temporary_dir="$(mktemp -d -t bcmetals-tls.XXXXXX)"
case "$temporary_dir" in
  /tmp/bcmetals-tls.*) ;;
  *) echo "ERROR: directorio temporal inesperado" >&2; exit 1 ;;
esac
cleanup() { rm -rf -- "$temporary_dir"; }
trap cleanup EXIT

lower_name="${nas_name,,}"
cat > "$temporary_dir/server.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=IP:${nas_ip},DNS:${nas_name},DNS:${lower_name},DNS:${lower_name}.local
EOF

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out "$tls_dir/ca.key"
openssl req -x509 -new -sha256 -days 3650 \
  -key "$tls_dir/ca.key" \
  -subj "/CN=BCMETALS NAS Audit Internal CA/O=BCMETALS" \
  -out "$tls_dir/ca.crt"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$tls_dir/server.key"
openssl req -new -sha256 \
  -key "$tls_dir/server.key" \
  -subj "/CN=${nas_name}/O=BCMETALS" \
  -out "$temporary_dir/server.csr"
openssl x509 -req -sha256 -days 825 \
  -in "$temporary_dir/server.csr" \
  -CA "$tls_dir/ca.crt" \
  -CAkey "$tls_dir/ca.key" \
  -CAserial "$temporary_dir/ca.srl" \
  -CAcreateserial \
  -extfile "$temporary_dir/server.ext" \
  -out "$tls_dir/server.crt"
cat "$tls_dir/server.crt" "$tls_dir/ca.crt" > "$tls_dir/server-fullchain.crt"
printf 'ip=%s\ndns=%s\nhttps_port=8443\n' "$nas_ip" "$nas_name" > "$tls_dir/endpoint.conf"

chmod 600 "$tls_dir/ca.key"
chmod 640 "$tls_dir/server.key"
chmod 644 "$tls_dir/ca.crt" "$tls_dir/server.crt" "$tls_dir/server-fullchain.crt" "$tls_dir/endpoint.conf"
chown 0:101 "$tls_dir/server.key"
openssl verify -CAfile "$tls_dir/ca.crt" "$tls_dir/server.crt"

echo "PASS: certificado interno creado para IP=$nas_ip DNS=$nas_name"
echo "CA pública para instalar en clientes: $tls_dir/ca.crt"
echo "La clave privada de la CA permanece en data/tls y nunca debe publicarse."
