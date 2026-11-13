#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
project_id="60000000-0000-4000-8000-000000000001"
other_project_id="60000000-0000-4000-8000-000000000002"
network_id="61000000-0000-4000-8000-000000000001"
other_network_id="61000000-0000-4000-8000-000000000002"
feature_id="62000000-0000-4000-8000-000000000001"
empty_feature_id="62000000-0000-4000-8000-000000000003"
other_feature_id="62000000-0000-4000-8000-000000000004"
missing_feature_id="62000000-0000-4000-8000-000000000099"

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$feature_id" \
  > "$response_file"

jq --exit-status '
  .id == "62000000-0000-4000-8000-000000000001"
  and .networkId == "61000000-0000-4000-8000-000000000001"
  and .networkName == "Spatial query target network"
  and .networkType == "test"
  and .sourceId == "inside"
  and .sourceLayer == "SPATIAL-QUERY-TEST"
  and .sourceType == "TestLineString"
  and .properties == {"position":"inside"}
  and (.createdAt | type) == "string"
' "$response_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$empty_feature_id" \
  > "$response_file"

jq --exit-status '.properties == {}' "$response_file" > /dev/null

assert_not_found() {
  local label="$1"
  local url="$2"
  local status

  status="$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' "$url")"
  if [[ "$status" != "404" ]]; then
    echo "$label: expected HTTP 404, received $status" >&2
    cat "$response_file" >&2
    return 1
  fi
}

assert_not_found \
  "missing feature" \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$missing_feature_id"

assert_not_found \
  "cross-network feature" \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$other_feature_id"

assert_not_found \
  "cross-project feature" \
  "$base_url/api/projects/$other_project_id/networks/$other_network_id/features/$feature_id"

echo "Feature details API integration checks passed."
