#!/usr/bin/env bash
# Importação pelo site (M4.5): envia um .geomap.json para POST /api/imports, como o botão "Importar" faz,
# importa de novo para provar que a rede é substituída sem duplicar, confere o histórico no banco e
# confirma que um arquivo inválido volta com o código do contrato sem gravar nada.
set -euo pipefail

base_url="${1:-http://localhost:8080}"
example="contracts/geomap/0.3/examples/valid-geoautoengine-blocks.geomap.json"
invalid="contracts/geomap/0.1/examples/invalid-crs.geomap.json"
response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

post() {
  curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
    --header 'Content-Type: application/json' \
    --data-binary "@$1" \
    "$base_url/api/imports"
}

status="$(post "$example")"
test "$status" = "200" || { cat "$response_file"; exit 1; }
jq --exit-status '.success == true and .received == 2 and .imported == 2 and .replaced == 0
  and (.projectId | type) == "string" and (.networkId | type) == "string"' "$response_file"

status="$(post "$example")"
test "$status" = "200" || { cat "$response_file"; exit 1; }
jq --exit-status '.success == true and .imported == 2 and .replaced == 2' "$response_file"

script_dir="$(cd "$(dirname "$0")" && pwd)"
"$script_dir/verify-network-imports.sh" "Águas pluviais (blocos)" 0.3 "GeoAutoEngine" 31982 metre \
  "$example" 2 2

status="$(post "$invalid")"
test "$status" = "400" || { cat "$response_file"; exit 1; }
jq --exit-status '.success == false and ([.errors[].code] | index("GMC_CRS_INVALID")) != null' "$response_file"

echo "Importação pelo site verificada."
