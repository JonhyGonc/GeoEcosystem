#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"
fixture_project_id="60000000-0000-4000-8000-000000000001"
other_project_id="60000000-0000-4000-8000-000000000002"
unknown_project_id="60000000-0000-4000-8000-000000000099"

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects" \
  > "$response_file"

jq --exit-status \
  --arg fixture "$fixture_project_id" \
  --arg other "$other_project_id" \
  --arg unknown "$unknown_project_id" '
  (.projects | type) == "array"
  and (.projects | length) >= 2
  and ([.projects[] | keys] | all(. == ["id", "name"]))
  and ([.projects[].id] | length) == ([.projects[].id] | unique | length)
  and ([.projects[] | select(.id == $fixture)] == [{
    "id": $fixture,
    "name": "Spatial query fixture project"
  }])
  and ([.projects[] | select(.id == $other)] == [{
    "id": $other,
    "name": "Spatial query other project"
  }])
  and ([.projects[].id] | index($unknown) | not)
  and ([.projects[].id] | index($fixture)) < ([.projects[].id] | index($other))
' "$response_file" > /dev/null

echo "Project catalog API integration checks passed."
