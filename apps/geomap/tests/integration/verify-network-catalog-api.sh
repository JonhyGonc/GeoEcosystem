#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
project_id="60000000-0000-4000-8000-000000000001"
other_project_id="60000000-0000-4000-8000-000000000002"
unknown_project_id="60000000-0000-4000-8000-000000000099"

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks" \
  > "$response_file"

jq --exit-status '
  .projectId == "60000000-0000-4000-8000-000000000001"
  and (.networks | length) == 2
  and ([.networks[].id] == [
    "61000000-0000-4000-8000-000000000002",
    "61000000-0000-4000-8000-000000000001"
  ])
  and ([.networks[].name] == [
    "Spatial query other network",
    "Spatial query target network"
  ])
  and ([.networks[].type] == ["stormwater", "test"])
  and ([.networks[].color] == ["#3366CC", null])
  and ([.networks[].id] | index("61000000-0000-4000-8000-000000000003") | not)
' "$response_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$other_project_id/networks" \
  > "$response_file"

jq --exit-status '
  .projectId == "60000000-0000-4000-8000-000000000002"
  and .networks == [{
    "id": "61000000-0000-4000-8000-000000000003",
    "name": "Empty network from other project",
    "type": "test",
    "color": null
  }]
' "$response_file" > /dev/null

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$unknown_project_id/networks" \
  > "$response_file"

jq --exit-status '
  .projectId == "60000000-0000-4000-8000-000000000099"
  and .networks == []
' "$response_file" > /dev/null

echo "Network catalog API integration checks passed."
