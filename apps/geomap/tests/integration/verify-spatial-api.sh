#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
project_id="60000000-0000-4000-8000-000000000001"
other_project_id="60000000-0000-4000-8000-000000000002"
network_id="61000000-0000-4000-8000-000000000001"
query="minLongitude=-48.86&minLatitude=-26.31&maxLongitude=-48.83&maxLatitude=-26.28"

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features?$query&limit=10" \
  > "$response_file"

jq --exit-status '
  .projectId == "60000000-0000-4000-8000-000000000001"
  and .networkId == "61000000-0000-4000-8000-000000000001"
  and .returned == 2
  and .hasMore == false
  and (([.features[].id] | sort) == [
    "62000000-0000-4000-8000-000000000001",
    "62000000-0000-4000-8000-000000000002"
  ])
  and ([.features[].geometry.type] | all(. == "LineString"))
' "$response_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features?$query&limit=1" \
  > "$response_file"

jq --exit-status '.returned == 1 and .limit == 1 and .hasMore == true' \
  "$response_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$other_project_id/networks/$network_id/features?$query&limit=10" \
  > "$response_file"

jq --exit-status '.returned == 0 and .hasMore == false' \
  "$response_file" > /dev/null

assert_bad_request() {
  local label="$1"
  local url="$2"
  local status

  status="$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' "$url")"
  if [[ "$status" != "400" ]]; then
    echo "$label: expected HTTP 400, received $status" >&2
    cat "$response_file" >&2
    return 1
  fi
}

assert_bad_request \
  "missing coordinate" \
  "$base_url/api/projects/$project_id/networks/$network_id/features?minLongitude=-48.86&minLatitude=-26.31&maxLongitude=-48.83"

assert_bad_request \
  "reversed longitude" \
  "$base_url/api/projects/$project_id/networks/$network_id/features?minLongitude=-48.83&minLatitude=-26.31&maxLongitude=-48.86&maxLatitude=-26.28"

assert_bad_request \
  "excessive span" \
  "$base_url/api/projects/$project_id/networks/$network_id/features?minLongitude=-49.5&minLatitude=-26.31&maxLongitude=-48.0&maxLatitude=-26.28"

assert_bad_request \
  "excessive limit" \
  "$base_url/api/projects/$project_id/networks/$network_id/features?$query&limit=2001"

echo "Spatial API integration checks passed."
