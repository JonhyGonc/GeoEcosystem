#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
tile_file="$(mktemp)"
catalog_file="$(mktemp)"
details_file="$(mktemp)"
trap 'rm -f "$tile_file" "$catalog_file" "$details_file"' EXIT

ids="$(docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA' <<'SQL'
WITH selected AS (
    SELECT
        project.id AS project_id,
        network.id AS network_id,
        feature.id AS feature_id,
        ST_Transform(ST_PointOnSurface(feature.geometry), 3857) AS point
    FROM projects project
    JOIN networks network ON network.project_id = project.id
    JOIN network_features feature ON feature.network_id = network.id
    WHERE project.name = 'Projeto exemplo'
      AND network.name = 'Águas pluviais'
      AND feature.source_id = '2A7F'
)
SELECT
    project_id::text || '|' ||
    network_id::text || '|' ||
    feature_id::text || '|' ||
    floor((ST_X(point) + 20037508.342789244) / 40075016.685578488 * 262144)::bigint || '|' ||
    floor((20037508.342789244 - ST_Y(point)) / 40075016.685578488 * 262144)::bigint
FROM selected;
SQL
)"

IFS='|' read -r project_id network_id feature_id tile_x tile_y <<< "$ids"
if [[ -z "$project_id" || -z "$network_id" || -z "$feature_id" || -z "$tile_x" || -z "$tile_y" ]]; then
  echo "Could not resolve the imported GeoAutoEngine fixture identifiers" >&2
  exit 1
fi

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks" \
  > "$catalog_file"
jq --exit-status --arg network_id "$network_id" '
  any(.networks[]; .id == $network_id and .name == "Águas pluviais" and .type == "stormwater")
' "$catalog_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$feature_id" \
  > "$details_file"
jq --exit-status --arg feature_id "$feature_id" '
  .id == $feature_id
  and .sourceId == "2A7F"
  and .sourceLayer == "GF-Rede Pluvial"
  and .sourceType == "Polyline"
  and .properties.cadHandle == "2A7F"
  and .properties.cadEntityType == "Polyline"
  and .properties.vertexCount == 2
  and .properties.closed == false
  and .properties.projectedTo2D == false
' "$details_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/tiles/18/$tile_x/$tile_y.mvt" \
  > "$tile_file"
python3 tests/integration/validate-mvt.py "$tile_file" --expected-id "$feature_id"

echo "GeoAutoEngine export passed importer, catalog, details and MVT checks."
