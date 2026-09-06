#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 4 ]]; then
  echo "Usage: benchmark-spatial-api.sh <base-url> <dataset> <project-id> <network-id>" >&2
  exit 2
fi

base_url="$1"
dataset="$2"
project_id="$3"
network_id="$4"
query="minLongitude=-48.90&minLatitude=-26.35&maxLongitude=-48.85&maxLatitude=-26.30&limit=500"
url="$base_url/api/projects/$project_id/networks/$network_id/features?$query"

response_file="$(mktemp)"
timings_file="$(mktemp)"
sorted_timings_file="$(mktemp)"
trap 'rm -f "$response_file" "$timings_file" "$sorted_timings_file"' EXIT

curl --fail --silent --show-error --max-time 10 "$url" > "$response_file"

returned="$(jq --exit-status '.returned | numbers' "$response_file")"
has_more="$(jq --raw-output --exit-status '.hasMore | booleans | tostring' "$response_file")"
if [[ "$returned" -lt 1 || "$returned" -gt 500 ]]; then
  echo "Unexpected returned count for $dataset: $returned" >&2
  exit 1
fi

for _ in $(seq 1 30); do
  curl --fail --silent --show-error --max-time 10 \
    --output /dev/null \
    --write-out '%{time_total}\n' \
    "$url" >> "$timings_file"
done

LC_ALL=C sort --numeric-sort "$timings_file" > "$sorted_timings_file"

awk -v dataset="$dataset" -v returned="$returned" -v has_more="$has_more" '
  { values[NR] = $1 }
  END {
    lower = values[int((NR + 1) / 2)]
    upper = values[int((NR + 2) / 2)]
    median = (lower + upper) / 2
    p95_index = int((NR * 95 + 99) / 100)
    printf "SPATIAL_API_BENCHMARK dataset=%s samples=%d returned=%s has_more=%s median_ms=%.3f p95_ms=%.3f max_ms=%.3f\n", dataset, NR, returned, has_more, median * 1000, values[p95_index] * 1000, values[NR] * 1000
  }
' "$sorted_timings_file"
