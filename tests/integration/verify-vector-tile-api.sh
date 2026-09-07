#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
project_id="60000000-0000-4000-8000-000000000001"
other_project_id="60000000-0000-4000-8000-000000000002"
network_id="61000000-0000-4000-8000-000000000001"
other_network_id="61000000-0000-4000-8000-000000000002"
inside_id="62000000-0000-4000-8000-000000000001"
crossing_id="62000000-0000-4000-8000-000000000002"
other_network_feature_id="62000000-0000-4000-8000-000000000004"

tile_file="$(mktemp)"
headers_file="$(mktemp)"
trap 'rm -f "$tile_file" "$headers_file"' EXIT

request_tile() {
  local path="$1"
  local expected_status="$2"
  local status

  status="$(curl --silent --show-error \
    --output "$tile_file" \
    --dump-header "$headers_file" \
    --write-out '%{http_code}' \
    "$base_url$path")"

  if [[ "$status" != "$expected_status" ]]; then
    echo "$path: expected HTTP $expected_status, received $status" >&2
    cat "$tile_file" >&2
    return 1
  fi
}

target_prefix="/api/projects/$project_id/networks/$network_id/tiles"

request_tile "$target_prefix/14/5969/9433.mvt" 200
grep --ignore-case --quiet '^content-type: application/vnd.mapbox-vector-tile' "$headers_file"
grep --ignore-case --quiet '^cache-control: public, max-age=60' "$headers_file"
python3 tests/integration/validate-mvt.py "$tile_file" \
  --expected-id "$inside_id" \
  --expected-id "$crossing_id"

request_tile "$target_prefix/16/23876/37732.mvt" 200
python3 tests/integration/validate-mvt.py "$tile_file" \
  --expected-id "$inside_id" \
  --expected-id "$crossing_id"

request_tile "/api/projects/$project_id/networks/$other_network_id/tiles/16/23876/37732.mvt" 200
python3 tests/integration/validate-mvt.py "$tile_file" \
  --expected-id "$other_network_feature_id"

request_tile "/api/projects/$other_project_id/networks/$network_id/tiles/16/23876/37732.mvt" 204
request_tile "$target_prefix/16/0/0.mvt" 204
request_tile "$target_prefix/13/0/0.mvt" 400
request_tile "$target_prefix/23/0/0.mvt" 400
request_tile "$target_prefix/14/16384/0.mvt" 400
request_tile "$target_prefix/14/0/-1.mvt" 400

echo "Vector tile API integration checks passed."
