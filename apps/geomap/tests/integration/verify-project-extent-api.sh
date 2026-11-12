#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
fixture_project_id="60000000-0000-4000-8000-000000000001"
empty_project_id="60000000-0000-4000-8000-000000000002"
unknown_project_id="60000000-0000-4000-8000-000000000099"

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

fetch() {
  curl --fail-with-body --silent --show-error --max-time 10 \
    "$base_url/api/projects/$1/extent" > "$response_file"
}

fetch "$fixture_project_id"
jq --exit-status --arg id "$fixture_project_id" '
  def near($a; $b): (($a - $b) | fabs) < 0.0001;
  .projectId == $id
  and (.extent | keys) == ["maxLatitude", "maxLongitude", "minLatitude", "minLongitude"]
  and near(.extent.minLongitude; -48.90)
  and near(.extent.minLatitude; -26.35)
  and near(.extent.maxLongitude; -48.71)
  and near(.extent.maxLatitude; -26.14)
  and (.focus | keys) == ["latitude", "longitude"]
  and near(.focus.longitude; -48.80)
  and near(.focus.latitude; -26.25)
' "$response_file" > /dev/null

for project_id in "$empty_project_id" "$unknown_project_id"; do
  fetch "$project_id"
  jq --exit-status --arg id "$project_id" \
    '.projectId == $id and .extent == null and .focus == null' "$response_file" > /dev/null
done

echo "Project extent API integration checks passed."
