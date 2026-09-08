#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:8080}"

large_ids="$(docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA -v expected_dataset=large -v expected_seed=424242' \
  < tests/integration/get-dataset-ids.sql)"
IFS='|' read -r project_id network_id <<< "$large_ids"

feature_id="$(docker compose exec -T database sh -ec \
  "psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -tAc \"SELECT id FROM network_features WHERE network_id = '$network_id' ORDER BY id LIMIT 1;\"")"

if [[ -z "$project_id" || -z "$network_id" || -z "$feature_id" ]]; then
  echo "Could not resolve the LARGE project, network and feature identifiers." >&2
  exit 1
fi

curl --fail-with-body --silent --show-error \
  "$base_url/api/projects/$project_id/networks/$network_id/features/$feature_id" |
  jq --exit-status \
    --arg project_id "$project_id" \
    --arg network_id "$network_id" \
    --arg feature_id "$feature_id" \
    '.projectId == $project_id
      and .networkId == $network_id
      and .id == $feature_id
      and (.properties | type) == "object"' \
    > /dev/null

echo "LARGE feature details check passed."
