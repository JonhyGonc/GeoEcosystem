#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 3 ]]; then
  echo "Usage: benchmark-project-extent.sh <base-url> <dataset> <project-id>" >&2
  exit 2
fi

base_url="$1"
dataset="$2"
project_id="$3"
url="$base_url/api/projects/$project_id/extent"
max_p95_ms="${GEOMAP_EXTENT_MAX_P95_MS:-2000}"

timings_file="$(mktemp)"
sorted_timings_file="$(mktemp)"
trap 'rm -f "$timings_file" "$sorted_timings_file"' EXIT

curl --fail --silent --show-error --max-time 10 "$url" \
  | jq --exit-status '
      .extent.minLongitude < .extent.maxLongitude
      and .extent.minLatitude < .extent.maxLatitude
      and .focus.longitude >= .extent.minLongitude and .focus.longitude <= .extent.maxLongitude
      and .focus.latitude >= .extent.minLatitude and .focus.latitude <= .extent.maxLatitude
    ' > /dev/null

for _ in $(seq 1 30); do
  curl --fail --silent --show-error --max-time 10 \
    --output /dev/null \
    --write-out '%{time_total}\n' \
    "$url" >> "$timings_file"
done

LC_ALL=C sort --numeric-sort "$timings_file" > "$sorted_timings_file"

awk -v dataset="$dataset" -v max_p95="$max_p95_ms" '
  { values[NR] = $1 }
  END {
    lower = values[int((NR + 1) / 2)]
    upper = values[int((NR + 2) / 2)]
    median = (lower + upper) / 2
    p95_index = int((NR * 95 + 99) / 100)
    p95 = values[p95_index] * 1000
    printf "PROJECT_EXTENT_BENCHMARK dataset=%s samples=%d median_ms=%.3f p95_ms=%.3f max_ms=%.3f\n", dataset, NR, median * 1000, p95, values[NR] * 1000
    if (p95 > max_p95) {
      printf "Project extent p95 %.3f ms exceeds %d ms for %s\n", p95, max_p95, dataset > "/dev/stderr"
      exit 1
    }
  }
' "$sorted_timings_file"
