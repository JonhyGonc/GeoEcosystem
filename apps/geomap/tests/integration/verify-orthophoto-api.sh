#!/usr/bin/env bash
# Ortofoto (DEC-035): coloca um MBTiles sintético com o nome do projeto na pasta montada na API e confere a
# situação da camada e um tile pela API, como o site faz.
set -euo pipefail

base_url="${1:-http://localhost:8080}"
project_name="Projeto exemplo"
folder="private-data/ortofotos"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

project_id="$(docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "$1"' \
  sh "SELECT id FROM projects WHERE name = '$project_name';")"
test -n "$project_id"

curl --fail-with-body --silent --show-error "$base_url/api/projects/$project_id/orthophoto" > "$work/before.json"
jq --exit-status --arg name "$project_name.mbtiles" '.available == false and .fileName == $name' "$work/before.json"

python3 - "$work/orto.mbtiles" <<'PY'
import base64, sqlite3, sys
png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGD4DwABBAEAwS2OUAAAAABJRU5ErkJggg==")
db = sqlite3.connect(sys.argv[1])
db.executescript("CREATE TABLE metadata (name text, value text); CREATE TABLE tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob);")
db.executemany("INSERT INTO metadata VALUES (?, ?)", [("format", "png"), ("minzoom", "0"), ("maxzoom", "0"), ("bounds", "-48.9,-26.4,-48.8,-26.3")])
db.execute("INSERT INTO tiles VALUES (0, 0, 0, ?)", (png,))
db.commit()
PY
# A pasta é criada pelo Docker (dono root) quando a API sobe.
sudo mkdir -p "$folder"
sudo cp "$work/orto.mbtiles" "$folder/$project_name.mbtiles"

# A API guarda a busca do arquivo por 30 segundos.
for attempt in {1..20}; do
  curl --fail-with-body --silent --show-error "$base_url/api/projects/$project_id/orthophoto" > "$work/after.json"
  if jq --exit-status '.available == true' "$work/after.json" > /dev/null; then
    break
  fi
  sleep 3
done
jq --exit-status '.available == true and .minZoom == 0 and .maxZoom == 0 and .bounds == [-48.9,-26.4,-48.8,-26.3]' "$work/after.json"

status="$(curl --silent --output "$work/tile.png" --write-out '%{http_code} %{content_type}' \
  "$base_url/api/projects/$project_id/orthophoto/tiles/0/0/0")"
test "$status" = "200 image/png"
cmp "$work/tile.png" <(python3 -c 'import base64,sys; sys.stdout.buffer.write(base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGD4DwABBAEAwS2OUAAAAABJRU5ErkJggg=="))')

echo "Ortofoto verificada."
